using ProcurementService.Core.Enums;

namespace ProcurementService.Core.Entities;

/// <summary>
/// P2 (LPO steps 1–2) — PURCHASE_REQUISITION. Every purchase at QSL begins here. Submission runs a hard
/// budget check (Finance seam); an approved PR routes to Procurement and triggers the P3 quotation process.
/// The Dept-Head must review within a 2-business-day SLA (breach escalates to the MD).
/// </summary>
public class PurchaseRequisition : BaseEntity
{
    public string PrNumber { get; set; } = string.Empty;   // PR-{yr}-{seq}
    public string RequestedBy { get; set; } = string.Empty;
    public string? RequestedByName { get; set; }
    public string DepartmentId { get; set; } = string.Empty;

    /// <summary>The Finance budget ("budget line") this PR draws against — verified at submission.</summary>
    public string? BudgetId { get; set; }
    public string? BudgetName { get; set; }

    public string? Justification { get; set; }
    public decimal TotalEstimated { get; set; }

    public PrStatus Status { get; set; } = PrStatus.Draft;

    public DateTime? SubmittedAt { get; set; }
    public DateTime? SlaDueAt { get; set; }        // SubmittedAt + 2 business days
    public DateTime? EscalatedAt { get; set; }     // set when an overdue PR is escalated to MD

    // Dept-Head review
    public string? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? RejectionReason { get; set; }

    // Forward links (populated by later phases)
    public string? QuotationComparisonId { get; set; }   // P3
    public string? PurchaseOrderId { get; set; }         // P4

    // P8 — post-hoc PR for an emergency PO
    public bool IsPostHoc { get; set; }
    public string? LinkedEmergencyPoId { get; set; }

    public ICollection<PurchaseRequisitionLine> Lines { get; set; } = new List<PurchaseRequisitionLine>();
}
