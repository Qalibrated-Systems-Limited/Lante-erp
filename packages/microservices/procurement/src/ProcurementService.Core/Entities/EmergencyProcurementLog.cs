namespace ProcurementService.Core.Entities;

/// <summary>
/// P8 (PROC-004) — EMERGENCY_PROCUREMENT_LOG. The control record for a purchase made under operational
/// urgency. Emergency procurement waives <b>only</b> the quotation requirement: the MD must approve before
/// anything is bought (never retrospectively), the written justification and quotation-waiver document are
/// mandatory, a post-hoc requisition must follow within 24 hours, and every emergency is reported in the
/// monthly board pack. GRN, the 3-way match and the payment-authority matrix all still apply, because an
/// emergency LPO is an ordinary <see cref="PurchaseOrder"/> that simply skipped sourcing.
/// </summary>
public class EmergencyProcurementLog : BaseEntity
{
    public string PoId { get; set; } = string.Empty;
    public string PoNumber { get; set; } = string.Empty;

    public string DeclaredBy { get; set; } = string.Empty;
    public DateTime DeclaredAt { get; set; } = DateTime.UtcNow;
    /// <summary>Mandatory written justification for the emergency.</summary>
    public string EmergencyReason { get; set; } = string.Empty;

    // ── MD authorisation (must precede the purchase) ──
    public string? MdApprovalRef { get; set; }
    public string? MdApprovedBy { get; set; }
    public DateTime? MdApprovedAt { get; set; }

    // ── Quotation waiver (mandatory document) ──
    public string QuotationWaiverUrl { get; set; } = string.Empty;
    public string WaiverReason { get; set; } = string.Empty;

    // ── Post-hoc requisition, due within 24h of declaration ──
    public DateTime PostHocDueAt { get; set; }
    public string? PostHocPrId { get; set; }
    public string? PostHocPrNumber { get; set; }
    public DateTime? PostHocRaisedAt { get; set; }

    /// <summary>The monthly board pack this emergency was reported in (e.g. "2026-07"); null until reported.</summary>
    public string? BoardPackPeriod { get; set; }
    public DateTime? BoardPackMarkedAt { get; set; }
}
