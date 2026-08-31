using AutoMapper;
using Microsoft.EntityFrameworkCore;
using ProcurementService.Core.DTOs.Requisitions;
using ProcurementService.Core.Entities;
using ProcurementService.Core.Enums;
using ProcurementService.Core.Interfaces.Repositories;
using ProcurementService.Core.Interfaces.Services;

namespace ProcurementService.Core.Services;

/// <summary>P2 — Purchase Requisition workflow. Draft → Submit (hard budget check) → Dept-Head review
/// (2-business-day SLA, breach → MD escalation). Every action is written to PR_APPROVAL_LOG so any
/// downstream LPO traces back to its originating request.</summary>
public class PurchaseRequisitionService(
    IGenericRepository<PurchaseRequisition> prs,
    IGenericRepository<PrApprovalLog> logs,
    IBudgetGateway budget,
    IMapper mapper) : IPurchaseRequisitionService
{
    private const int SlaBusinessDays = 2;

    public async Task<PrListResult> GetAllAsync(PrFilterParams filter)
    {
        var q = prs.Query().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(filter.Status) && Enum.TryParse<PrStatus>(filter.Status, true, out var st))
            q = q.Where(x => x.Status == st);
        if (!string.IsNullOrWhiteSpace(filter.DepartmentId))
            q = q.Where(x => x.DepartmentId == filter.DepartmentId);

        var total = await q.CountAsync();
        var items = await q.OrderByDescending(x => x.CreatedAt)
            .Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize).ToListAsync();

        var now = DateTime.UtcNow;
        var dtos = items.Select(x =>
        {
            var d = mapper.Map<PrSummaryRowDto>(x);
            d.IsOverdue = IsOverdue(x, now);
            return d;
        }).ToList();
        if (filter.OverdueOnly == true) dtos = dtos.Where(d => d.IsOverdue).ToList();
        return new PrListResult(dtos, total);
    }

    public async Task<PrReadDto?> GetByIdAsync(string id)
    {
        var pr = await prs.Query().AsNoTracking().Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == id);
        if (pr is null) return null;
        var dto = mapper.Map<PrReadDto>(pr);
        dto.IsOverdue = IsOverdue(pr, DateTime.UtcNow);
        return dto;
    }

    public async Task<PrReadDto> CreateAsync(CreatePrDto dto, string userId, string? userName)
    {
        if (string.IsNullOrWhiteSpace(dto.DepartmentId)) throw new InvalidOperationException("Department is required.");
        if (dto.Lines.Count == 0) throw new InvalidOperationException("A requisition needs at least one line.");

        // PrNumber is unique-indexed; GenerateNumberAsync's count-then-format is racy under
        // concurrent creates, so retry with a freshly counted number on collision.
        const int maxAttempts = 3;
        for (var attempt = 1; ; attempt++)
        {
            var pr = new PurchaseRequisition
            {
                PrNumber = await GenerateNumberAsync(),
                RequestedBy = userId,
                RequestedByName = userName,
                DepartmentId = dto.DepartmentId,
                BudgetId = dto.BudgetId,
                BudgetName = dto.BudgetName,
                Justification = dto.Justification,
                Status = PrStatus.Draft,
                CreatedBy = userId,
                UpdatedBy = userId,
                Lines = dto.Lines.Select(l => new PurchaseRequisitionLine
                {
                    ItemDescription = l.ItemDescription.Trim(),
                    Quantity = l.Quantity,
                    Unit = l.Unit,
                    EstimatedUnitPrice = l.EstimatedUnitPrice,
                    LineTotal = l.Quantity * l.EstimatedUnitPrice,
                    CreatedBy = userId,
                    UpdatedBy = userId,
                }).ToList(),
            };
            pr.TotalEstimated = pr.Lines.Sum(l => l.LineTotal);
            try
            {
                var created = await prs.CreateAsync(pr);
                return (await GetByIdAsync(created.Id))!;
            }
            catch (DbUpdateException) when (attempt < maxAttempts)
            {
                // retry with the next attempt's freshly generated number
            }
        }
    }

    public async Task<PrReadDto?> UpdateAsync(string id, CreatePrDto dto, string userId)
    {
        var pr = await prs.Query().Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == id);
        if (pr is null) return null;
        if (pr.Status != PrStatus.Draft) throw new InvalidOperationException("Only a draft requisition can be edited.");

        pr.DepartmentId = dto.DepartmentId;
        pr.BudgetId = dto.BudgetId;
        pr.BudgetName = dto.BudgetName;
        pr.Justification = dto.Justification;
        pr.Lines.Clear();
        foreach (var l in dto.Lines)
            pr.Lines.Add(new PurchaseRequisitionLine
            {
                PrId = pr.Id,
                ItemDescription = l.ItemDescription.Trim(),
                Quantity = l.Quantity,
                Unit = l.Unit,
                EstimatedUnitPrice = l.EstimatedUnitPrice,
                LineTotal = l.Quantity * l.EstimatedUnitPrice,
                CreatedBy = userId,
                UpdatedBy = userId,
            });
        pr.TotalEstimated = pr.Lines.Sum(l => l.LineTotal);
        Touch(pr, userId);
        await prs.UpdateAsync(pr);
        return await GetByIdAsync(id);
    }

    public async Task<PrActionResult> SubmitAsync(string id, string userId, string? userName)
    {
        var pr = await prs.GetByIdAsync(id);
        if (pr is null) return new PrActionResult("Error", "Requisition not found.");
        if (pr.Status != PrStatus.Draft) return new PrActionResult("Error", "Only a draft requisition can be submitted.");

        // Hard budget block (LPO step 1). Fail-open when Finance isn't wired.
        var check = await budget.CheckAsync(pr.BudgetId, pr.TotalEstimated);
        if (!check.Available)
            return new PrActionResult("Blocked", $"Budget block — {check.Message}");
        if (check.BudgetName != null && string.IsNullOrWhiteSpace(pr.BudgetName)) pr.BudgetName = check.BudgetName;

        pr.Status = PrStatus.PendingDeptHead;
        pr.SubmittedAt = DateTime.UtcNow;
        pr.SlaDueAt = AddBusinessDays(pr.SubmittedAt.Value, SlaBusinessDays);
        Touch(pr, userId);
        await prs.UpdateAsync(pr);
        await LogAsync(pr.Id, PrAction.Submitted, userId, userName, "Submitted for Dept-Head review.");
        return new PrActionResult("PendingDeptHead", "Requisition submitted for approval.");
    }

    public async Task<PrActionResult> ReviewAsync(string id, ReviewPrDto dto, string userId, string? userName)
    {
        var pr = await prs.GetByIdAsync(id);
        if (pr is null) return new PrActionResult("Error", "Requisition not found.");
        if (pr.Status != PrStatus.PendingDeptHead) return new PrActionResult("Error", "Only a pending requisition can be reviewed.");

        pr.ReviewedBy = userId;
        pr.ReviewedAt = DateTime.UtcNow;
        if (dto.Approve)
        {
            pr.Status = PrStatus.Approved;
            Touch(pr, userId);
            await prs.UpdateAsync(pr);
            await LogAsync(pr.Id, PrAction.Approved, userId, userName, "Approved by Dept-Head.");
            return new PrActionResult("Approved", "Requisition approved — routed to Procurement for quotation.");
        }
        if (string.IsNullOrWhiteSpace(dto.Reason))
            return new PrActionResult("Error", "A rejection reason is mandatory.");
        pr.Status = PrStatus.Rejected;
        pr.RejectionReason = dto.Reason;
        Touch(pr, userId);
        await prs.UpdateAsync(pr);
        await LogAsync(pr.Id, PrAction.Rejected, userId, userName, dto.Reason);
        return new PrActionResult("Rejected", "Requisition rejected.");
    }

    public async Task<PrActionResult> EscalateAsync(string id, string userId)
    {
        var pr = await prs.GetByIdAsync(id);
        if (pr is null) return new PrActionResult("Error", "Requisition not found.");
        if (pr.Status != PrStatus.PendingDeptHead) return new PrActionResult("Error", "Only a pending requisition can be escalated.");
        pr.EscalatedAt = DateTime.UtcNow;
        Touch(pr, userId);
        await prs.UpdateAsync(pr);
        await LogAsync(pr.Id, PrAction.Escalated, userId, null, "SLA breach — escalated to MD.");
        return new PrActionResult("Escalated", "Requisition escalated to MD.");
    }

    public async Task<PrSummaryDto> GetSummaryAsync()
    {
        var now = DateTime.UtcNow;
        var all = await prs.Query().AsNoTracking().ToListAsync();
        return new PrSummaryDto
        {
            Total = all.Count,
            Draft = all.Count(x => x.Status == PrStatus.Draft),
            PendingDeptHead = all.Count(x => x.Status == PrStatus.PendingDeptHead),
            Approved = all.Count(x => x.Status == PrStatus.Approved),
            Rejected = all.Count(x => x.Status == PrStatus.Rejected),
            Overdue = all.Count(x => IsOverdue(x, now)),
            PendingValue = all.Where(x => x.Status == PrStatus.PendingDeptHead).Sum(x => x.TotalEstimated),
        };
    }

    // ── Helpers ──
    private static bool IsOverdue(PurchaseRequisition pr, DateTime now)
        => pr.Status == PrStatus.PendingDeptHead && pr.SlaDueAt is { } due && due < now;

    private static DateTime AddBusinessDays(DateTime from, int days)
    {
        var d = from;
        while (days > 0)
        {
            d = d.AddDays(1);
            if (d.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday) days--;
        }
        return d;
    }

    private async Task<string> GenerateNumberAsync()
    {
        var prefix = $"PR-{DateTime.UtcNow.Year}-";
        var count = await prs.Query().CountAsync(x => x.PrNumber.StartsWith(prefix));
        return $"{prefix}{(count + 1):D4}";
    }

    private async Task LogAsync(string prId, PrAction action, string userId, string? userName, string? reason)
    {
        await logs.CreateAsync(new PrApprovalLog
        {
            PrId = prId,
            ApproverId = userId,
            ApproverName = userName,
            Action = action,
            Reason = reason,
            ActionedAt = DateTime.UtcNow,
            CreatedBy = userId,
            UpdatedBy = userId,
        });
    }

    private static void Touch(BaseEntity e, string userId) { e.UpdatedBy = userId; e.UpdatedAt = DateTime.UtcNow; }
}
