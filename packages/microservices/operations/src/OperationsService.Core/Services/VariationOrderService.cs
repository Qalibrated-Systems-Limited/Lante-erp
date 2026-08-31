using AutoMapper;
using Microsoft.EntityFrameworkCore;
using OperationsService.Core.DTOs.Variations;
using OperationsService.Core.Entities;
using OperationsService.Core.Enums;
using OperationsService.Core.Interfaces.Repositories;
using OperationsService.Core.Interfaces.Services;

namespace OperationsService.Core.Services;

/// <summary>O7 — see <see cref="IVariationOrderService"/>.</summary>
public class VariationOrderService(
    IGenericRepository<VariationOrder> vos,
    IGenericRepository<VariationOrderLine> lines,
    IGenericRepository<Project> projects,
    IGenericRepository<BudgetLine> budgetLines,
    IFinanceGateway finance,
    IMapper mapper) : IVariationOrderService
{
    // ── Reads ────────────────────────────────────────────────────────────────

    public async Task<VariationOrderReadDto?> GetByIdAsync(string id)
    {
        var vo = await vos.Query()
            .Include(v => v.Lines.Where(l => !l.IsDeleted))
            .FirstOrDefaultAsync(v => v.Id == id && !v.IsDeleted);
        return vo is null ? null : mapper.Map<VariationOrderReadDto>(vo);
    }

    public async Task<IEnumerable<VariationOrderReadDto>> GetByProjectAsync(string projectId)
    {
        var items = await vos.Query()
            .Include(v => v.Lines.Where(l => !l.IsDeleted))
            .Where(v => v.ProjectId == projectId && !v.IsDeleted)
            .OrderByDescending(v => v.CreatedAt)
            .ToListAsync();
        return mapper.Map<List<VariationOrderReadDto>>(items);
    }

    // ── Draft management ───────────────────────────────────────────────────────

    public async Task<VariationOrderReadDto> CreateAsync(CreateVariationOrderDto dto, string userId)
    {
        _ = await projects.GetByIdAsync(dto.ProjectId)
            ?? throw new KeyNotFoundException($"Project {dto.ProjectId} not found.");
        if (dto.Lines.Count == 0)
            throw new InvalidOperationException("A variation order needs at least one line.");

        var vo = new VariationOrder
        {
            ProjectId      = dto.ProjectId,
            Number         = await GenerateNumberAsync(),
            Title          = dto.Title,
            Description    = dto.Description,
            Reason         = dto.Reason,
            Status         = VariationOrderStatus.Draft,
            IsBillable     = dto.IsBillable,
            BudgetCategory = dto.BudgetCategory,
            VatRate        = dto.VatRate <= 0 ? 0.16m : dto.VatRate,
            RequestedBy    = userId,
            CreatedBy      = userId,
            UpdatedBy      = userId,
        };
        await vos.CreateAsync(vo);
        await ReplaceLinesAsync(vo, dto.Lines, userId);
        await RecomputeTotalsAsync(vo, userId);
        return (await GetByIdAsync(vo.Id))!;
    }

    public async Task<VariationOrderReadDto> UpdateAsync(string id, UpdateVariationOrderDto dto, string userId)
    {
        var vo = await LoadDraftAsync(id);

        if (dto.Title != null) vo.Title = dto.Title;
        if (dto.Description != null) vo.Description = dto.Description;
        if (dto.Reason != null) vo.Reason = dto.Reason;
        if (dto.IsBillable.HasValue) vo.IsBillable = dto.IsBillable.Value;
        if (dto.BudgetCategory.HasValue) vo.BudgetCategory = dto.BudgetCategory.Value;
        if (dto.VatRate.HasValue) vo.VatRate = dto.VatRate.Value <= 0 ? 0.16m : dto.VatRate.Value;
        vo.UpdatedBy = userId;
        vo.UpdatedAt = DateTime.UtcNow;
        await vos.UpdateAsync(vo);

        if (dto.Lines != null) await ReplaceLinesAsync(vo, dto.Lines, userId);
        await RecomputeTotalsAsync(vo, userId);
        return (await GetByIdAsync(vo.Id))!;
    }

    public async Task DeleteAsync(string id, string userId)
    {
        var vo = await LoadDraftAsync(id);
        vo.IsDeleted = true;
        vo.UpdatedBy = userId;
        vo.UpdatedAt = DateTime.UtcNow;
        await vos.UpdateAsync(vo);
    }

    // ── Approval workflow ──────────────────────────────────────────────────────

    public async Task<VariationOrderReadDto> SubmitForApprovalAsync(string id, string userId)
    {
        var vo = await vos.GetByIdAsync(id) ?? throw new KeyNotFoundException($"Variation order {id} not found.");
        if (vo.Status != VariationOrderStatus.Draft)
            throw new InvalidOperationException($"Only a draft variation order can be submitted. Current status: {vo.Status}.");
        if (vo.TotalAmount <= 0m)
            throw new InvalidOperationException("A variation order must have a non-zero value before submission.");

        vo.Status = VariationOrderStatus.PendingMdApproval;
        Stamp(vo, userId);
        await vos.UpdateAsync(vo);
        return (await GetByIdAsync(vo.Id))!;
    }

    public async Task<VariationOrderReadDto> MdReviewAsync(string id, ReviewVariationOrderDto dto, string userId)
    {
        var vo = await vos.GetByIdAsync(id) ?? throw new KeyNotFoundException($"Variation order {id} not found.");
        if (vo.Status != VariationOrderStatus.PendingMdApproval)
            throw new InvalidOperationException($"Variation order is not awaiting MD approval. Current status: {vo.Status}.");

        if (dto.Approved)
        {
            vo.Status       = VariationOrderStatus.PendingClientApproval;
            vo.MdApprovedBy = userId;
            vo.MdApprovedAt = DateTime.UtcNow;
        }
        else
        {
            vo.Status = VariationOrderStatus.Rejected;
            vo.RejectionReason = dto.Comments;
        }
        Stamp(vo, userId);
        await vos.UpdateAsync(vo);
        return (await GetByIdAsync(vo.Id))!;
    }

    public async Task<VariationOrderReadDto> ClientApproveAsync(string id, ClientApproveVariationOrderDto dto, string userId)
    {
        if (string.IsNullOrWhiteSpace(dto.ClientApprovedBy))
            throw new InvalidOperationException("The approving client representative's name is required.");

        var vo = await vos.GetByIdAsync(id) ?? throw new KeyNotFoundException($"Variation order {id} not found.");
        if (vo.Status != VariationOrderStatus.PendingClientApproval)
            throw new InvalidOperationException($"Variation order is not awaiting client approval. Current status: {vo.Status}.");

        vo.Status           = VariationOrderStatus.Approved;
        vo.ClientApprovedBy = dto.ClientApprovedBy.Trim();
        vo.ClientApprovedAt = DateTime.UtcNow;
        Stamp(vo, userId);
        await vos.UpdateAsync(vo);

        // O7 — apply the approved change: grow the contract value + planned budget, add a budget line,
        // and (if billable) raise a Finance invoice.
        var project = await projects.GetByIdAsync(vo.ProjectId);
        if (project != null)
        {
            var line = await budgetLines.CreateAsync(new BudgetLine
            {
                ProjectId     = project.Id,
                Category      = vo.BudgetCategory,
                Description   = $"Variation {vo.Number}: {vo.Title}",
                PlannedAmount = vo.Subtotal,
                CreatedBy     = userId,
                UpdatedBy     = userId,
            });

            project.ContractValue += vo.TotalAmount;
            project.PlannedBudget += vo.Subtotal;
            project.UpdatedBy = userId;
            project.UpdatedAt = DateTime.UtcNow;
            await projects.UpdateAsync(project);

            vo.BudgetLineId = line.Id;
        }

        vo.AppliedAt = DateTime.UtcNow;
        await vos.UpdateAsync(vo);

        if (vo.IsBillable && vo.TotalAmount > 0m)
        {
            try
            {
                // Client details come from the project so finance can match the same customer the
                // milestone invoices (and CRM) already use, rather than creating a duplicate.
                var invoiceProject = await projects.GetByIdAsync(vo.ProjectId);
                await finance.RaiseVariationInvoiceAsync(new VariationInvoiceRequest(
                    vo.ProjectId, vo.Id, vo.Number, vo.TotalAmount, userId,
                    invoiceProject?.ClientName, invoiceProject?.ClientId));
            }
            catch (Exception) { /* seam is best-effort; the VO is already applied */ }
        }

        return (await GetByIdAsync(vo.Id))!;
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private async Task<VariationOrder> LoadDraftAsync(string id)
    {
        var vo = await vos.GetByIdAsync(id) ?? throw new KeyNotFoundException($"Variation order {id} not found.");
        if (vo.Status != VariationOrderStatus.Draft)
            throw new InvalidOperationException($"Only a draft variation order can be edited. Current status: {vo.Status}.");
        return vo;
    }

    private async Task ReplaceLinesAsync(VariationOrder vo, List<VariationOrderLineDto> newLines, string userId)
    {
        var existing = await lines.Query().Where(l => l.VariationOrderId == vo.Id && !l.IsDeleted).ToListAsync();
        foreach (var old in existing)
        {
            old.IsDeleted = true;
            old.UpdatedBy = userId;
            old.UpdatedAt = DateTime.UtcNow;
            await lines.UpdateAsync(old);
        }
        foreach (var l in newLines)
        {
            await lines.CreateAsync(new VariationOrderLine
            {
                VariationOrderId = vo.Id,
                Description = l.Description,
                Quantity    = l.Quantity,
                UnitPrice   = l.UnitPrice,
                Amount      = Math.Round(l.Quantity * l.UnitPrice, 2),
                CreatedBy   = userId,
                UpdatedBy   = userId,
            });
        }
    }

    private async Task RecomputeTotalsAsync(VariationOrder vo, string userId)
    {
        var current = await lines.Query().Where(l => l.VariationOrderId == vo.Id && !l.IsDeleted).ToListAsync();
        vo.Subtotal    = current.Sum(l => l.Amount);
        vo.VatAmount   = Math.Round(vo.Subtotal * vo.VatRate, 2);
        vo.TotalAmount = vo.Subtotal + vo.VatAmount;
        Stamp(vo, userId);
        await vos.UpdateAsync(vo);
    }

    private static void Stamp(VariationOrder vo, string userId)
    {
        vo.UpdatedBy = userId;
        vo.UpdatedAt = DateTime.UtcNow;
    }

    private async Task<string> GenerateNumberAsync()
    {
        var prefix = $"VO-{DateTime.UtcNow.Year}-";
        var count = await vos.Query().CountAsync(v => v.Number.StartsWith(prefix));
        return $"{prefix}{(count + 1):D4}";
    }
}
