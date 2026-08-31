using AutoMapper;
using Microsoft.EntityFrameworkCore;
using ProcurementService.Core.DTOs.Emergency;
using ProcurementService.Core.DTOs.PurchaseOrders;
using ProcurementService.Core.Entities;
using ProcurementService.Core.Enums;
using ProcurementService.Core.Interfaces.Repositories;
using ProcurementService.Core.Interfaces.Services;

namespace ProcurementService.Core.Services;

/// <summary>
/// P8 (PROC-004) — emergency procurement. Waives sourcing and nothing else.
/// <para>The LPO is raised without a requisition or comparison, but it is created <b>PendingApproval with a
/// single MD step</b> regardless of value: the MD must authorise before the purchase is made, so approval can
/// never be retrospective. MD authorisation is what issues the LPO, and it is captured with a mandatory
/// approval reference. Justification and the quotation-waiver document are mandatory at declaration; a
/// post-hoc requisition is due within 24 hours (lateness is recorded and reported, not blocked); every
/// declaration is flagged for the monthly board pack.</para>
/// <para>Because the result is an ordinary <see cref="PurchaseOrder"/>, the P5 goods receipt, the P6 3-way
/// match and Finance's payment-authority matrix continue to apply untouched — which is exactly what PROC-004
/// requires. The board-resolution threshold above 500k also still applies.</para>
/// </summary>
public class EmergencyProcurementService(
    IGenericRepository<EmergencyProcurementLog> logs,
    IGenericRepository<PurchaseOrder> pos,
    IGenericRepository<PurchaseRequisition> prs,
    IGenericRepository<PurchaseRequisitionLine> prLines,
    IGenericRepository<PrApprovalLog> prApprovals,
    IGenericRepository<Supplier> suppliers,
    IGenericRepository<ProcurementAuditLog> audit,
    IPurchaseOrderService purchaseOrders,
    IMapper mapper) : IEmergencyProcurementService
{
    private const decimal BoardResolutionThreshold = 500_000m;
    private static readonly TimeSpan PostHocWindow = TimeSpan.FromHours(24);

    // ── Reads ──
    public async Task<EmergencyListResult> GetAllAsync(EmergencyFilterParams filter)
    {
        var q = logs.Query().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(filter.Period) && TryPeriod(filter.Period, out var from, out var to))
            q = q.Where(x => x.DeclaredAt >= from && x.DeclaredAt < to);

        var now = DateTime.UtcNow;
        q = filter.Flag switch
        {
            "AwaitingMd" => q.Where(x => x.MdApprovedAt == null),
            "PostHocOutstanding" => q.Where(x => x.PostHocPrId == null),
            "BoardPackPending" => q.Where(x => x.BoardPackPeriod == null),
            _ => q,
        };

        var total = await q.CountAsync();
        var items = await q.OrderByDescending(x => x.DeclaredAt)
            .Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize).ToListAsync();

        var poIds = items.Select(x => x.PoId).ToList();
        var poInfo = await pos.Query().AsNoTracking().Where(p => poIds.Contains(p.Id))
            .Select(p => new { p.Id, p.SupplierName, p.TotalAmount, p.Status }).ToListAsync();

        var rows = mapper.Map<List<EmergencyRowDto>>(items);
        foreach (var row in rows)
        {
            var log = items.First(x => x.Id == row.Id);
            var po = poInfo.FirstOrDefault(p => p.Id == row.PoId);
            row.SupplierName = po?.SupplierName;
            row.TotalAmount = po?.TotalAmount ?? 0m;
            row.PoStatus = (po?.Status ?? PoStatus.Draft).ToString();
            row.MdApproved = log.MdApprovedAt != null;
            row.PostHocOverdue = log.PostHocPrId is null && now > log.PostHocDueAt;
            row.PostHocRaisedLate = log.PostHocRaisedAt != null && log.PostHocRaisedAt > log.PostHocDueAt;
        }
        return new EmergencyListResult(rows, total);
    }

    public async Task<EmergencyReadDto?> GetByIdAsync(string id)
    {
        var log = await logs.Query().AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        return log is null ? null : await ToDtoAsync(log);
    }

    public async Task<EmergencyReadDto?> GetByPoAsync(string poId)
    {
        var log = await logs.Query().AsNoTracking().FirstOrDefaultAsync(x => x.PoId == poId);
        return log is null ? null : await ToDtoAsync(log);
    }

    public async Task<EmergencySummaryDto> GetSummaryAsync()
    {
        var all = await logs.Query().AsNoTracking().ToListAsync();
        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var poIds = all.Select(x => x.PoId).ToList();
        var values = await pos.Query().AsNoTracking().Where(p => poIds.Contains(p.Id))
            .Select(p => new { p.Id, p.TotalAmount }).ToListAsync();
        decimal ValueOf(string poId) => values.FirstOrDefault(v => v.Id == poId)?.TotalAmount ?? 0m;

        return new EmergencySummaryDto
        {
            Total = all.Count,
            AwaitingMdApproval = all.Count(x => x.MdApprovedAt == null),
            PostHocOutstanding = all.Count(x => x.PostHocPrId is null),
            PostHocOverdue = all.Count(x => x.PostHocPrId is null && now > x.PostHocDueAt),
            PostHocRaisedLate = all.Count(x => x.PostHocRaisedAt != null && x.PostHocRaisedAt > x.PostHocDueAt),
            BoardPackPending = all.Count(x => x.BoardPackPeriod is null),
            TotalValue = all.Sum(x => ValueOf(x.PoId)),
            ValueThisMonth = all.Where(x => x.DeclaredAt >= monthStart).Sum(x => ValueOf(x.PoId)),
        };
    }

    public async Task<List<EmergencyRowDto>> GetBoardPackAsync(string period)
    {
        var r = await GetAllAsync(new EmergencyFilterParams { Period = period, PageSize = 500 });
        return r.Items;
    }

    // ── Declaration ──
    public async Task<EmergencyActionResult> DeclareAsync(DeclareEmergencyDto dto, string userId)
    {
        if (string.IsNullOrWhiteSpace(dto.EmergencyReason))
            return Err("A written justification for the emergency is mandatory.");
        if (string.IsNullOrWhiteSpace(dto.QuotationWaiverUrl))
            return Err("The quotation-waiver document is mandatory for an emergency purchase.");
        if (string.IsNullOrWhiteSpace(dto.WaiverReason))
            return Err("State why the quotation requirement is being waived.");
        if (dto.TotalAmount <= 0) return Err("The purchase value must be greater than zero.");

        // The ASR gate is not waived — only sourcing is.
        var supplier = await suppliers.GetByIdAsync(dto.SupplierId);
        if (supplier is null || !supplier.IsApproved || supplier.BlacklistFlag)
            return Err("The supplier must be ASR-approved and not blacklisted.");

        var declaredAt = DateTime.UtcNow;
        var po = await pos.CreateAsync(new PurchaseOrder
        {
            PoNumber = await GeneratePoNumberAsync(),
            PrId = string.Empty,                 // no requisition yet — the post-hoc one follows within 24h
            QuotationComparisonId = null,        // sourcing waived
            SupplierId = supplier.Id,
            SupplierName = supplier.Name,
            TotalAmount = dto.TotalAmount,
            Currency = "KES",
            Band = BandFor(dto.TotalAmount),     // recorded for reporting; the MD is the approver regardless
            Status = PoStatus.PendingApproval,
            BoardResolutionRequired = dto.TotalAmount > BoardResolutionThreshold,
            IsEmergency = true,
            EmergencyReason = dto.EmergencyReason,
            QuotationWaiver = true,
            CreatedBy = userId,
            UpdatedBy = userId,
            // Emergencies always go to the MD, whatever the value — that is the PROC-004 control.
            Approvals = new List<PoApproval>
            {
                new() { Sequence = 1, Role = ApprovalRole.MD, Status = ApprovalStepStatus.Pending, CreatedBy = userId, UpdatedBy = userId },
            },
        });

        var log = await logs.CreateAsync(new EmergencyProcurementLog
        {
            PoId = po.Id,
            PoNumber = po.PoNumber,
            DeclaredBy = userId,
            DeclaredAt = declaredAt,
            EmergencyReason = dto.EmergencyReason,
            QuotationWaiverUrl = dto.QuotationWaiverUrl,
            WaiverReason = dto.WaiverReason,
            PostHocDueAt = declaredAt.Add(PostHocWindow),
            CreatedBy = userId,
            UpdatedBy = userId,
        });

        await LogAsync(log.Id, AsrAuditAction.EmergencyDeclared,
            $"Emergency procurement declared for {po.PoNumber} ({dto.TotalAmount:N2} KES, {supplier.Name}): {dto.EmergencyReason} Quotation waived: {dto.WaiverReason}. Post-hoc requisition due {log.PostHocDueAt:u}.", userId);
        return new EmergencyActionResult("Declared",
            $"Emergency declared — {po.PoNumber} awaits MD authorisation before the purchase may be made. Post-hoc requisition due within 24 hours.", log.Id);
    }

    // ── MD authorisation (the control that permits the purchase) ──
    public async Task<EmergencyActionResult> MdApproveAsync(string id, MdApproveEmergencyDto dto, string userId, string? userName)
    {
        if (string.IsNullOrWhiteSpace(dto.MdApprovalRef))
            return Err("The MD's authorisation reference is mandatory.");

        var log = await logs.GetByIdAsync(id);
        if (log is null) return Err("Emergency record not found.");
        if (log.MdApprovedAt != null) return Err("This emergency has already been authorised by the MD.");

        // Reuse the LPO approval machinery so the board-resolution gate above 500k and issuing behave
        // identically to a normal LPO; the chain here is the single MD step created at declaration.
        var signed = await purchaseOrders.SignAsync(log.PoId,
            new SignLpoDto { Approve = true, Notes = $"Emergency authorisation {dto.MdApprovalRef}. {dto.Notes}".Trim() },
            userId, userName, viaEmergencyAuthorisation: true);
        if (signed.Status == "Error") return Err(signed.Message);

        log.MdApprovalRef = dto.MdApprovalRef;
        log.MdApprovedBy = userId;
        log.MdApprovedAt = DateTime.UtcNow;
        Touch(log, userId);
        await logs.UpdateAsync(log);

        var po = await pos.GetByIdAsync(log.PoId);
        if (po is not null)
        {
            po.EmergencyApprovedBy = userId;
            po.EmergencyApprovedAt = log.MdApprovedAt;
            Touch(po, userId);
            await pos.UpdateAsync(po);
        }

        await LogAsync(id, AsrAuditAction.EmergencyMdApproved,
            $"MD authorised emergency {log.PoNumber} (ref {dto.MdApprovalRef}). {signed.Message}", userId);
        return new EmergencyActionResult("MdApproved", $"MD authorisation recorded. {signed.Message}", id);
    }

    // ── Post-hoc requisition (due within 24h) ──
    public async Task<EmergencyActionResult> RaisePostHocPrAsync(string id, PostHocPrDto dto, string userId)
    {
        var log = await logs.GetByIdAsync(id);
        if (log is null) return Err("Emergency record not found.");
        if (log.PostHocPrId != null) return Err($"A post-hoc requisition ({log.PostHocPrNumber}) already exists.");

        var po = await pos.GetByIdAsync(log.PoId);
        if (po is null) return Err("The emergency LPO no longer exists.");

        var raisedAt = DateTime.UtcNow;
        var lines = dto.Lines.Count > 0
            ? dto.Lines
            : new List<PostHocLineDto>
            {
                new() { ItemDescription = $"Emergency purchase — {log.PoNumber}", Quantity = 1, EstimatedUnitPrice = po.TotalAmount },
            };
        var total = lines.Sum(l => l.Quantity * l.EstimatedUnitPrice);

        // Ratification of a purchase the MD already authorised, so it lands Approved rather than re-running
        // the dept-head SLA — but it is flagged post-hoc and linked to the emergency for the audit trail.
        var pr = await prs.CreateAsync(new PurchaseRequisition
        {
            PrNumber = await GeneratePrNumberAsync(),
            RequestedBy = userId,
            DepartmentId = string.IsNullOrWhiteSpace(dto.DepartmentId) ? "Unassigned" : dto.DepartmentId,
            Justification = dto.Justification ?? $"Post-hoc requisition for emergency {log.PoNumber}: {log.EmergencyReason}",
            TotalEstimated = total,
            Status = PrStatus.Approved,
            SubmittedAt = raisedAt,
            ReviewedBy = userId,
            ReviewedAt = raisedAt,
            IsPostHoc = true,
            LinkedEmergencyPoId = log.PoId,
            PurchaseOrderId = log.PoId,
            CreatedBy = userId,
            UpdatedBy = userId,
        });

        foreach (var l in lines)
            await prLines.CreateAsync(new PurchaseRequisitionLine
            {
                PrId = pr.Id,
                ItemDescription = l.ItemDescription,
                Quantity = l.Quantity,
                Unit = l.Unit,
                EstimatedUnitPrice = l.EstimatedUnitPrice,
                LineTotal = l.Quantity * l.EstimatedUnitPrice,
                CreatedBy = userId,
                UpdatedBy = userId,
            });

        var late = raisedAt > log.PostHocDueAt;
        await prApprovals.CreateAsync(new PrApprovalLog
        {
            PrId = pr.Id,
            Action = PrAction.Approved,
            ApproverId = userId,
            Reason = $"Post-hoc ratification of emergency {log.PoNumber}"
                   + (late ? $" — raised {(raisedAt - log.PostHocDueAt).TotalHours:N1}h after the 24-hour window." : " — within the 24-hour window."),
            ActionedAt = raisedAt,
            CreatedBy = userId,
            UpdatedBy = userId,
        });

        log.PostHocPrId = pr.Id;
        log.PostHocPrNumber = pr.PrNumber;
        log.PostHocRaisedAt = raisedAt;
        Touch(log, userId);
        await logs.UpdateAsync(log);

        // Close the spine: the emergency LPO now points at its ratifying requisition.
        po.PrId = pr.Id;
        Touch(po, userId);
        await pos.UpdateAsync(po);

        await LogAsync(id, AsrAuditAction.EmergencyPostHocPrRaised,
            $"Post-hoc requisition {pr.PrNumber} raised for emergency {log.PoNumber}"
            + (late ? $" LATE — {(raisedAt - log.PostHocDueAt).TotalHours:N1}h past the 24-hour window." : " within the 24-hour window."), userId);

        return new EmergencyActionResult(late ? "RaisedLate" : "Raised",
            late
                ? $"Post-hoc requisition {pr.PrNumber} raised {(raisedAt - log.PostHocDueAt).TotalHours:N1}h late — the breach is recorded for board reporting."
                : $"Post-hoc requisition {pr.PrNumber} raised within the 24-hour window.", id);
    }

    // ── Board reporting ──
    public async Task<EmergencyActionResult> MarkBoardPackAsync(string id, BoardPackDto dto, string userId)
    {
        if (!TryPeriod(dto.Period, out _, out _))
            return Err("Give the board-pack period as YYYY-MM.");

        var log = await logs.GetByIdAsync(id);
        if (log is null) return Err("Emergency record not found.");

        log.BoardPackPeriod = dto.Period;
        log.BoardPackMarkedAt = DateTime.UtcNow;
        Touch(log, userId);
        await logs.UpdateAsync(log);
        await LogAsync(id, AsrAuditAction.EmergencyBoardPackMarked,
            $"Emergency {log.PoNumber} reported in the {dto.Period} board pack.", userId);
        return new EmergencyActionResult("Reported", $"Included in the {dto.Period} board pack.", id);
    }

    // ── Helpers ──
    private async Task<EmergencyReadDto> ToDtoAsync(EmergencyProcurementLog log)
    {
        var dto = mapper.Map<EmergencyReadDto>(log);
        var po = await pos.Query().AsNoTracking().FirstOrDefaultAsync(p => p.Id == log.PoId);
        dto.SupplierName = po?.SupplierName;
        dto.TotalAmount = po?.TotalAmount ?? 0m;
        dto.PoStatus = (po?.Status ?? PoStatus.Draft).ToString();
        dto.ReceiptStatus = (po?.ReceiptStatus ?? PoReceiptStatus.NotReceived).ToString();

        var now = DateTime.UtcNow;
        dto.PostHocOverdue = log.PostHocPrId is null && now > log.PostHocDueAt;
        dto.PostHocRaisedLate = log.PostHocRaisedAt != null && log.PostHocRaisedAt > log.PostHocDueAt;
        dto.AwaitingMdApproval = log.MdApprovedAt is null;
        dto.BoardPackPending = log.BoardPackPeriod is null;
        return dto;
    }

    private static bool TryPeriod(string? period, out DateTime from, out DateTime to)
    {
        from = default; to = default;
        if (string.IsNullOrWhiteSpace(period)) return false;
        var parts = period.Split('-');
        if (parts.Length != 2 || !int.TryParse(parts[0], out var y) || !int.TryParse(parts[1], out var m)) return false;
        if (y < 2000 || y > 2200 || m < 1 || m > 12) return false;
        from = new DateTime(y, m, 1, 0, 0, 0, DateTimeKind.Utc);
        to = from.AddMonths(1);
        return true;
    }

    private static SourcingBand BandFor(decimal total)
        => total <= 10_000m ? SourcingBand.DirectLpo
         : total <= 100_000m ? SourcingBand.OneQuote
         : total <= BoardResolutionThreshold ? SourcingBand.TwoQuotes
         : SourcingBand.ThreeQuotesMd;

    private async Task<string> GeneratePoNumberAsync()
    {
        var prefix = $"LPO-{DateTime.UtcNow.Year}-";
        var count = await pos.Query().CountAsync(x => x.PoNumber.StartsWith(prefix));
        return $"{prefix}{(count + 1):D4}";
    }

    private async Task<string> GeneratePrNumberAsync()
    {
        var prefix = $"PR-{DateTime.UtcNow.Year}-";
        var count = await prs.Query().CountAsync(x => x.PrNumber.StartsWith(prefix));
        return $"{prefix}{(count + 1):D4}";
    }

    private static EmergencyActionResult Err(string message) => new("Error", message);
    private static void Touch(BaseEntity e, string userId) { e.UpdatedBy = userId; e.UpdatedAt = DateTime.UtcNow; }

    private async Task LogAsync(string entityId, AsrAuditAction action, string detail, string userId)
    {
        await audit.CreateAsync(new ProcurementAuditLog
        {
            EntityType = "EmergencyProcurement", EntityId = entityId, Action = action,
            Detail = detail, PerformedBy = userId, OccurredAt = DateTime.UtcNow,
            CreatedBy = userId, UpdatedBy = userId,
        });
    }
}
