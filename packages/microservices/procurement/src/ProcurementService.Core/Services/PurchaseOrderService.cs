using System.Security.Cryptography;
using System.Text;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using ProcurementService.Core.DTOs.PurchaseOrders;
using ProcurementService.Core.Entities;
using ProcurementService.Core.Enums;
using ProcurementService.Core.Interfaces.Repositories;
using ProcurementService.Core.Interfaces.Services;

namespace ProcurementService.Core.Services;

/// <summary>P4 — LPO generation &amp; approval. Builds the value-based approval chain (authority matrix),
/// enforces the Board-Resolution prerequisite above 500k, applies a digital signature per step, and on
/// final approval issues the LPO. The purchase commitment is a memo only — see SignAsync for why no
/// general-ledger entry is posted at LPO issue.</summary>
public class PurchaseOrderService(
    IGenericRepository<PurchaseOrder> pos,
    IGenericRepository<PurchaseRequisition> prs,
    IGenericRepository<QuotationComparison> comparisons,
    IGenericRepository<Quotation> quotations,
    IGenericRepository<Supplier> suppliers,
    IGenericRepository<ProcurementAuditLog> audit,
    IBoardResolutionGateway boardResolutions,
    IMapper mapper) : IPurchaseOrderService
{
    private const decimal Band1 = 10_000m, Band2 = 100_000m, Band3 = 500_000m;

    public async Task<PoListResult> GetAllAsync(PoFilterParams filter)
    {
        var q = pos.Query().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(filter.Status) && Enum.TryParse<PoStatus>(filter.Status, true, out var st)) q = q.Where(x => x.Status == st);
        if (!string.IsNullOrWhiteSpace(filter.SupplierId)) q = q.Where(x => x.SupplierId == filter.SupplierId);
        if (!string.IsNullOrWhiteSpace(filter.PrId)) q = q.Where(x => x.PrId == filter.PrId);
        var total = await q.CountAsync();
        var items = await q.OrderByDescending(x => x.CreatedAt).Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize).ToListAsync();
        return new PoListResult(mapper.Map<List<PoSummaryRowDto>>(items), total);
    }

    public async Task<PoReadDto?> GetByIdAsync(string id)
    {
        var po = await pos.Query().AsNoTracking().Include(x => x.Approvals).FirstOrDefaultAsync(x => x.Id == id);
        return po is null ? null : ToDto(po);
    }

    public async Task<PoReadDto?> GetByPrAsync(string prId)
    {
        var po = await pos.Query().AsNoTracking().Include(x => x.Approvals).FirstOrDefaultAsync(x => x.PrId == prId);
        return po is null ? null : ToDto(po);
    }

    public async Task<PoActionResult> GenerateAsync(string prId, GenerateLpoDto dto, string userId)
    {
        var pr = await prs.GetByIdAsync(prId);
        if (pr is null) return new PoActionResult("Error", "Requisition not found.");
        if (pr.Status != PrStatus.Approved) return new PoActionResult("Error", "The requisition must be approved.");
        if (string.IsNullOrEmpty(pr.QuotationComparisonId)) return new PoActionResult("Error", "Complete the quotation comparison first.");
        if (!string.IsNullOrEmpty(pr.PurchaseOrderId)) return new PoActionResult("Error", "An LPO already exists for this requisition.");

        // Two different bands, deliberately: the SOURCING band (how many quotes had to be sought) can only
        // ever come from the requisition estimate, since it is decided before any quote exists — but the
        // APPROVAL AUTHORITY band must follow the value actually being committed, which is the winning quote
        // or an explicit override, not the estimate.
        var sourcingBand = BandFor(pr.TotalEstimated);
        string supplierId; string? supplierName; decimal total;

        if (sourcingBand == SourcingBand.DirectLpo)
        {
            if (string.IsNullOrWhiteSpace(dto.SupplierId)) return new PoActionResult("Error", "Select a supplier for the direct LPO.");
            var s = await suppliers.GetByIdAsync(dto.SupplierId);
            if (s is null || !s.IsApproved || s.BlacklistFlag) return new PoActionResult("Error", "Supplier must be ASR-approved.");
            supplierId = s.Id; supplierName = s.Name; total = dto.TotalAmount ?? pr.TotalEstimated;
        }
        else
        {
            var cmp = await comparisons.Query().FirstOrDefaultAsync(c => c.PrId == prId);
            if (cmp?.RecommendedSupplierId is null) return new PoActionResult("Error", "No recommended supplier on the comparison.");
            supplierId = cmp.RecommendedSupplierId; supplierName = cmp.RecommendedSupplierName;
            var recTotal = cmp.RecommendedQuotationId is { } qid
                ? (await quotations.GetByIdAsync(qid))?.TotalQuoted : null;
            total = dto.TotalAmount ?? recTotal ?? pr.TotalEstimated;
        }

        var authorityBand = BandFor(total);

        // PoNumber is unique-indexed; GenerateNumberAsync's count-then-format is racy under
        // concurrent LPO generation for different requisitions, so retry with a freshly counted
        // number on collision instead of surfacing a raw 500.
        PurchaseOrder created = null!;
        const int maxAttempts = 3;
        for (var attempt = 1; ; attempt++)
        {
            var po = new PurchaseOrder
            {
                PoNumber = await GenerateNumberAsync(),
                PrId = prId,
                QuotationComparisonId = pr.QuotationComparisonId,
                SupplierId = supplierId,
                SupplierName = supplierName,
                TotalAmount = total,
                Currency = "KES",
                Band = authorityBand,
                Status = PoStatus.PendingApproval,
                BoardResolutionRequired = total > Band3,
                PromisedDeliveryDate = dto.PromisedDeliveryDate,
                CreatedBy = userId,
                UpdatedBy = userId,
                Approvals = BuildChain(authorityBand, userId),
            };
            try
            {
                created = await pos.CreateAsync(po);
                break;
            }
            catch (DbUpdateException) when (attempt < maxAttempts)
            {
                // retry with the next attempt's freshly generated number
            }
        }

        pr.PurchaseOrderId = created.Id;
        Touch(pr, userId);
        await prs.UpdateAsync(pr);

        var escalated = authorityBand > sourcingBand
            ? $" Approval escalated to {authorityBand} — the committed value exceeds the {sourcingBand} requisition estimate of {pr.TotalEstimated:N0}."
            : "";
        await LogAsync(created.Id,
            $"LPO {created.PoNumber} generated (sourced {sourcingBand}, approval {authorityBand}, {total:N0}).{escalated}", userId);
        return new PoActionResult("PendingApproval", $"LPO {created.PoNumber} generated — routed for approval.{escalated}");
    }

    public async Task<PoActionResult> RestateValueAsync(string poId, decimal newTotal, string currency, string userId)
    {
        var po = await pos.Query().Include(x => x.Approvals).FirstOrDefaultAsync(x => x.Id == poId);
        if (po is null) return new PoActionResult("Error", "LPO not found.");
        if (newTotal <= 0) return new PoActionResult("Error", "The restated value must be greater than zero.");
        if (po.Status is not (PoStatus.Draft or PoStatus.PendingApproval))
            return new PoActionResult("NotRestated", $"The LPO is already {po.Status}; its value stands as issued.");

        // Never rewrite the value under an approver who has already signed against the old figure.
        if (po.Approvals.Any(a => a.Status != ApprovalStepStatus.Pending))
            return new PoActionResult("NotRestated", "Approval has already begun on the current value — reject the LPO and regenerate it to change the value.");

        var oldTotal = po.TotalAmount;
        var oldBand = po.Band;
        var newBand = BandFor(newTotal);

        po.TotalAmount = newTotal;
        if (!string.IsNullOrWhiteSpace(currency)) po.Currency = currency;
        po.BoardResolutionRequired = newTotal > Band3;

        // The authority matrix is value-based, so a changed value must re-derive who signs. Nothing has been
        // actioned yet (guarded above), so the pending chain is safe to replace.
        if (newBand != oldBand)
        {
            foreach (var stale in po.Approvals.ToList()) po.Approvals.Remove(stale);
            foreach (var step in BuildChain(newBand, userId)) po.Approvals.Add(step);
            po.Band = newBand;
        }
        Touch(po, userId);
        await pos.UpdateAsync(po);

        var rebuilt = newBand != oldBand ? $" Approval chain rebuilt {oldBand} → {newBand}." : "";
        await LogAsync(poId, $"LPO value restated {oldTotal:N2} → {newTotal:N2} {po.Currency}.{rebuilt}", userId);
        return new PoActionResult("Restated", $"LPO value restated to {newTotal:N2} {po.Currency}.{rebuilt}");
    }

    public async Task<PoActionResult> AttachBoardResolutionAsync(string poId, BoardResolutionDto dto, string userId)
    {
        if (string.IsNullOrWhiteSpace(dto.ResolutionRef)) return new PoActionResult("Error", "Board resolution reference is required.");
        var po = await pos.GetByIdAsync(poId);
        if (po is null) return new PoActionResult("Error", "LPO not found.");

        // DEC-C — Compliance owns the resolution register (COMP-007), so check the reference against it.
        var check = await boardResolutions.VerifyAsync(dto.ResolutionRef);

        // Compliance answered and holds no such resolution: that is a mistyped or invented reference, and
        // accepting it would leave a >500k LPO resting on a resolution that does not exist.
        if (check.Reachable && !check.Found)
            return new PoActionResult("Error", check.Message);

        po.BoardResolutionRef = check.Found ? check.ReferenceNo ?? dto.ResolutionRef : dto.ResolutionRef;
        // Prefer the scanned copy Compliance holds; fall back to whatever the user supplied.
        po.BoardResolutionUrl = check.ScannedCopyUrl ?? dto.Url;
        po.BoardResolutionId = check.Id;
        po.BoardResolutionVerified = check.Found;
        Touch(po, userId);
        await pos.UpdateAsync(po);

        await LogAsync(poId,
            $"Board resolution {po.BoardResolutionRef} attached — {(check.Found ? "verified against Compliance" : $"UNVERIFIED ({check.Message})")}.", userId);
        return new PoActionResult("Ok",
            check.Found
                ? $"Board resolution attached and verified. {check.Message}"
                : $"Board resolution attached but NOT verified — {check.Message}");
    }

    public async Task<PoActionResult> SignAsync(string poId, SignLpoDto dto, string userId, string? userName, bool viaEmergencyAuthorisation = false)
    {
        var po = await pos.Query().Include(x => x.Approvals).FirstOrDefaultAsync(x => x.Id == poId);
        if (po is null) return new PoActionResult("Error", "LPO not found.");
        if (po.Status != PoStatus.PendingApproval) return new PoActionResult("Error", "This LPO is not awaiting approval.");

        // An emergency LPO may only be APPROVED through the emergency route, which captures the MD's mandatory
        // authorisation reference (PROC-004). Rejection needs no reference, so it stays open here.
        if (po.IsEmergency && dto.Approve && !viaEmergencyAuthorisation)
            return new PoActionResult("Error", "Authorise this emergency purchase via the emergency MD-approval endpoint so the MD's authorisation reference is recorded.");

        var next = po.Approvals.Where(a => a.Status == ApprovalStepStatus.Pending).OrderBy(a => a.Sequence).FirstOrDefault();
        if (next is null) return new PoActionResult("Error", "No pending approval step.");

        if (!dto.Approve)
        {
            if (string.IsNullOrWhiteSpace(dto.Reason)) return new PoActionResult("Error", "A rejection reason is mandatory.");
            next.Status = ApprovalStepStatus.Rejected;
            next.ApproverId = userId; next.ApproverName = userName; next.ActionedAt = DateTime.UtcNow; next.Notes = dto.Reason;
            po.Status = PoStatus.Rejected; po.RejectionReason = dto.Reason;
            Touch(po, userId); await pos.UpdateAsync(po);
            await LogAsync(poId, $"LPO rejected by {next.Role}: {dto.Reason}", userId);
            return new PoActionResult("Rejected", "LPO rejected — returned to Procurement.");
        }

        // Board-resolution gate: the MD cannot sign a >500k LPO until the resolution is attached.
        if (next.Role == ApprovalRole.MD && po.BoardResolutionRequired && string.IsNullOrWhiteSpace(po.BoardResolutionRef))
            return new PoActionResult("Error", "A Board Resolution must be attached before the MD can sign this LPO.");

        next.Status = ApprovalStepStatus.Approved;
        next.ApproverId = userId; next.ApproverName = userName;
        next.SignatureRef = Sign(userId, poId);
        next.ActionedAt = DateTime.UtcNow; next.Notes = dto.Notes;

        var remaining = po.Approvals.Any(a => a.Status == ApprovalStepStatus.Pending);
        if (remaining)
        {
            Touch(po, userId); await pos.UpdateAsync(po);
            await LogAsync(poId, $"{next.Role} signed LPO — awaiting next approver.", userId);
            return new PoActionResult("PendingApproval", $"Signed by {next.Role} — awaiting the next approver.");
        }

        // Fully approved → issue. The purchase commitment is recorded here as a memo only, deliberately with
        // NO general-ledger entry: an issued LPO is an executory contract, not yet a liability. The single AP
        // credit for the purchase is posted by Finance when it approves the supplier invoice (Dr expense +
        // Dr input VAT / Cr trade payables) — posting a commitment journal here as well would credit payables
        // twice for the same purchase. Commitment visibility comes from the LPO itself (value + status).
        po.Status = PoStatus.Approved; po.ApprovedAt = DateTime.UtcNow;
        po.Status = PoStatus.Issued; po.IssuedAt = DateTime.UtcNow;
        Touch(po, userId); await pos.UpdateAsync(po);
        await LogAsync(poId, $"LPO fully approved and issued. Commitment of {po.TotalAmount:N2} recorded (memo — no GL entry).", userId);
        return new PoActionResult("Issued", $"LPO approved and issued to supplier. Commitment of {po.TotalAmount:N2} recorded.");
    }

    public async Task<PoActionResult> RecordReceiptAsync(string poId, RecordReceiptDto dto, string userId)
    {
        var po = await pos.GetByIdAsync(poId);
        if (po is null) return new PoActionResult("Error", "LPO not found.");
        po.ReceivedQty += dto.AcceptedQty;
        po.RejectedQty += dto.RejectedQty;   // kept for the P9 quality score (GRN rejection rate)
        po.ReceiptStatus = dto.PartialDelivery ? PoReceiptStatus.PartiallyReceived : PoReceiptStatus.FullyReceived;
        po.ReceivedAt = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(dto.GrnId)) po.LastGrnRef = dto.GrnId;
        Touch(po, userId);
        await pos.UpdateAsync(po);
        await LogAsync(poId, $"Goods receipt recorded (accepted {dto.AcceptedQty}, rejected {dto.RejectedQty}, {(dto.PartialDelivery ? "partial" : "complete")}).", userId);
        return new PoActionResult("Ok", "Receipt recorded against the LPO.");
    }

    public async Task<PoSummaryDto> GetSummaryAsync()
    {
        var all = await pos.Query().AsNoTracking().ToListAsync();
        return new PoSummaryDto
        {
            Total = all.Count,
            PendingApproval = all.Count(x => x.Status == PoStatus.PendingApproval),
            Issued = all.Count(x => x.Status == PoStatus.Issued),
            Rejected = all.Count(x => x.Status == PoStatus.Rejected),
            AwaitingBoardResolution = all.Count(x => x.Status == PoStatus.PendingApproval && x.BoardResolutionRequired && string.IsNullOrEmpty(x.BoardResolutionRef)),
            IssuedValue = all.Where(x => x.Status == PoStatus.Issued).Sum(x => x.TotalAmount),
        };
    }

    // ── Helpers ──
    private static SourcingBand BandFor(decimal total)
        => total <= Band1 ? SourcingBand.DirectLpo
         : total <= Band2 ? SourcingBand.OneQuote
         : total <= Band3 ? SourcingBand.TwoQuotes
         : SourcingBand.ThreeQuotesMd;

    private static List<PoApproval> BuildChain(SourcingBand band, string userId)
    {
        var roles = band switch
        {
            SourcingBand.DirectLpo => new[] { ApprovalRole.ProcurementOfficer },
            SourcingBand.OneQuote => new[] { ApprovalRole.ProcurementManager },
            SourcingBand.TwoQuotes => new[] { ApprovalRole.FinanceManager, ApprovalRole.MD },
            SourcingBand.ThreeQuotesMd => new[] { ApprovalRole.MD },
            _ => new[] { ApprovalRole.ProcurementOfficer },
        };
        var seq = 1;
        return roles.Select(r => new PoApproval
        {
            Sequence = seq++,
            Role = r,
            Status = ApprovalStepStatus.Pending,
            CreatedBy = userId,
            UpdatedBy = userId,
        }).ToList();
    }

    private static string Sign(string userId, string poId)
    {
        var raw = $"{userId}:{poId}:{DateTime.UtcNow:O}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)))[..16];
    }

    private PoReadDto ToDto(PurchaseOrder po)
    {
        var dto = mapper.Map<PoReadDto>(po);
        dto.Approvals = po.Approvals.OrderBy(a => a.Sequence).Select(a => new PoApprovalDto
        {
            Id = a.Id, Sequence = a.Sequence, Role = a.Role.ToString(), Status = a.Status.ToString(),
            ApproverId = a.ApproverId, ApproverName = a.ApproverName, Signed = a.SignatureRef != null,
            ActionedAt = a.ActionedAt, Notes = a.Notes,
        }).ToList();
        dto.NextApprovalRole = po.Approvals.Where(a => a.Status == ApprovalStepStatus.Pending)
            .OrderBy(a => a.Sequence).FirstOrDefault()?.Role.ToString();
        return dto;
    }

    private async Task<string> GenerateNumberAsync()
    {
        var prefix = $"LPO-{DateTime.UtcNow.Year}-";
        var count = await pos.Query().CountAsync(x => x.PoNumber.StartsWith(prefix));
        return $"{prefix}{(count + 1):D4}";
    }

    private async Task LogAsync(string entityId, string detail, string userId)
    {
        await audit.CreateAsync(new ProcurementAuditLog
        {
            EntityType = "PurchaseOrder", EntityId = entityId, Action = AsrAuditAction.LpoIssued,
            Detail = detail, PerformedBy = userId, OccurredAt = DateTime.UtcNow, CreatedBy = userId, UpdatedBy = userId,
        });
    }

    private static void Touch(BaseEntity e, string userId) { e.UpdatedBy = userId; e.UpdatedAt = DateTime.UtcNow; }
}
