using ProcurementService.Core.Enums;

namespace ProcurementService.Core.Entities;

/// <summary>
/// P4 — PURCHASE_ORDER (LPO). The formal order raised from an approved PR + completed quotation comparison,
/// routed through the value-based approval authority matrix (each step digitally signed). LPOs above
/// KES 500,000 require a Board Resolution before the MD may sign. On final approval a purchase-commitment
/// journal is posted to Finance and the LPO is issued to the supplier.
/// </summary>
public class PurchaseOrder : BaseEntity
{
    public string PoNumber { get; set; } = string.Empty;   // LPO-{yr}-{seq}
    public string PrId { get; set; } = string.Empty;
    public string? QuotationComparisonId { get; set; }
    public string SupplierId { get; set; } = string.Empty;
    public string? SupplierName { get; set; }

    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "KES";

    /// <summary>The value band that determined the APPROVAL AUTHORITY, derived from <see cref="TotalAmount"/>
    /// — the value actually being committed. (The <i>sourcing</i> band, i.e. how many quotes had to be sought,
    /// comes from the requisition estimate and is persisted on the QuotationComparison; the two differ
    /// whenever the winning quote lands in a different band from the estimate.)</summary>
    public SourcingBand Band { get; set; }

    public PoStatus Status { get; set; } = PoStatus.Draft;

    // Board resolution (required when > 500,000). DEC-C: verified against Compliance's COMP-007 register when
    // that module is wired, otherwise captured locally and flagged unverified.
    public bool BoardResolutionRequired { get; set; }
    public string? BoardResolutionRef { get; set; }
    public string? BoardResolutionUrl { get; set; }
    /// <summary>True only when Compliance confirmed the reference. False means local capture — either
    /// Compliance is not wired or it could not be reached — so the MD is signing against an unverified
    /// resolution, which the LPO surfaces rather than hiding.</summary>
    public bool BoardResolutionVerified { get; set; }
    /// <summary>The Compliance BoardResolution record id, when verified.</summary>
    public string? BoardResolutionId { get; set; }

    public DateTime? ApprovedAt { get; set; }
    public DateTime? IssuedAt { get; set; }
    public string? RejectionReason { get; set; }

    /// <summary>Reserved. No GL entry is posted at LPO issue (the commitment is a memo — an LPO is an
    /// executory contract, and Finance posts the purchase's only AP credit when it approves the supplier
    /// invoice), so this stays null today. Kept for a later phase that does post a journal against an LPO.</summary>
    public string? CommitmentJournalRef { get; set; }

    // P5 — goods receipt (set by the Stores GRN callback, DEC-3).
    public PoReceiptStatus ReceiptStatus { get; set; } = PoReceiptStatus.NotReceived;
    public DateTime? ReceivedAt { get; set; }
    public string? LastGrnRef { get; set; }
    public decimal ReceivedQty { get; set; }
    /// <summary>Quantity Stores rejected at inspection, accumulated from the GRN callback. Feeds the P9
    /// quality score (GRN rejection rate); the callback has always sent it, it just was not kept.</summary>
    public decimal RejectedQty { get; set; }

    /// <summary>Date the supplier committed to deliver by. Optional, but it is what makes the P9 delivery
    /// score a true on-time percentage rather than a lead-time approximation.</summary>
    public DateTime? PromisedDeliveryDate { get; set; }

    // P8 — emergency procurement: the quotation step is waived, nothing else is. An emergency LPO has no
    // sourcing comparison and (initially) no requisition; the MD must approve it before the purchase is made.
    public bool IsEmergency { get; set; }
    public string? EmergencyReason { get; set; }
    public bool QuotationWaiver { get; set; }
    public string? EmergencyApprovedBy { get; set; }
    public DateTime? EmergencyApprovedAt { get; set; }

    public ICollection<PoApproval> Approvals { get; set; } = new List<PoApproval>();
}
