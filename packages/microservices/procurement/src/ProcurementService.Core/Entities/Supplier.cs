using ProcurementService.Core.Enums;

namespace ProcurementService.Core.Entities;

/// <summary>
/// P1 (PROC-001) — SUPPLIER. The authoritative Approved Supplier Register record. Procurement OWNS this
/// master (DEC-2); the finance AP vendor and stores goods supplier reference it by id. No supplier may be
/// used in a Purchase Order unless <c>IsApproved</c> and not <c>BlacklistFlag</c>.
/// </summary>
public class Supplier : BaseEntity
{
    public string SupplierNumber { get; set; } = string.Empty;   // SUP-{yr}-{seq}
    public string Name { get; set; } = string.Empty;
    public string? KraPin { get; set; }
    public string? CategoryId { get; set; }

    // Contact / address
    public string? ContactPerson { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }

    public SupplierStatus Status { get; set; } = SupplierStatus.Pending;

    // Approval (PROC-001) — Procurement Manager approves.
    public bool IsApproved { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }

    // Conflict of interest (PROC-007) — any QSL staff link?
    public bool ConflictChecked { get; set; }
    public bool ConflictFound { get; set; }
    public string? ConflictNotes { get; set; }
    public string? ConflictCheckedBy { get; set; }
    public DateTime? ConflictCheckedAt { get; set; }

    // Blacklist — MD approves, reason mandatory.
    public bool BlacklistFlag { get; set; }
    public string? BlacklistReason { get; set; }
    public string? BlacklistedBy { get; set; }
    public DateTime? BlacklistedAt { get; set; }

    // Performance (P9 stamps this; nullable until first review).
    public decimal? OverallScore { get; set; }
    public DateTime? LastReviewedAt { get; set; }

    // DEC-2 downstream references — the same supplier's id in finance/stores (set on seed/sync).
    public string? FinanceSupplierId { get; set; }
    public string? StoreSupplierId { get; set; }
    public string? Source { get; set; }   // "native" | "seed:finance" | "seed:stores"

    public ICollection<SupplierDocument> Documents { get; set; } = new List<SupplierDocument>();
}
