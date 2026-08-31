using CrmService.Core.Enums;

namespace CrmService.Core.Entities;

/// <summary>
/// P4 — Quotation (CRM-016/017/021/022, 052–054). Always linked to an opportunity; client details
/// auto-populate from the customer. Full version control — revising creates a new version and the
/// prior one is retained as Superseded. Approval: SE drafts → Dept Head → MD (if total exceeds the
/// threshold). 16% VAT. This is the SALES quotation, distinct from the operations calibration quote.
/// </summary>
public class Quotation : BaseEntity
{
    public string QuoteNumber { get; set; } = string.Empty;   // QT-{year}-{seq} (shared across versions)
    public int Version { get; set; } = 1;
    public bool IsCurrent { get; set; } = true;

    public string OpportunityId { get; set; } = string.Empty;
    public string? CustomerId { get; set; }
    public string? CustomerName { get; set; }

    public string Title { get; set; } = string.Empty;
    public DateTime IssueDate { get; set; } = DateTime.UtcNow;
    public DateTime? ValidUntil { get; set; }
    public string? Notes { get; set; }

    public decimal Subtotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal VatRate { get; set; } = 0.16m;
    public decimal VatAmount { get; set; }
    public decimal TotalAmount { get; set; }

    public QuotationStatus Status { get; set; } = QuotationStatus.Draft;

    // Approval trail.
    public string? SubmittedBy { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public string? DeptHeadApprovedBy { get; set; }
    public DateTime? DeptHeadApprovedAt { get; set; }
    public string? MdApprovedBy { get; set; }
    public DateTime? MdApprovedAt { get; set; }
    public string? RejectionReason { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime? DecidedAt { get; set; }   // accepted / rejected by client

    public ICollection<QuotationLine> Lines { get; set; } = new List<QuotationLine>();
}
