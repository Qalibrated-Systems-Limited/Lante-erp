using CrmService.Core.Enums;

namespace CrmService.Core.Entities;

/// <summary>
/// P5 — Tender &amp; bid management (CRM-018/019/020). Registered with source, client, estimated value,
/// submission deadline and bid-bond details. Deadline alerts fire at 14/7/3/1 days (SE + Head of BD).
/// Won tenders link to an opportunity (→ P3/P6); Lost records a reason.
/// </summary>
public class Tender : BaseEntity
{
    public string TenderNumber { get; set; } = string.Empty;   // TND-{year}-{seq}
    public string Title { get; set; } = string.Empty;
    public string? Source { get; set; }
    public string? CustomerId { get; set; }
    public string ClientName { get; set; } = string.Empty;   // tendering entity
    public string? Description { get; set; }

    public decimal EstimatedValue { get; set; }
    public DateTime SubmissionDeadline { get; set; }
    public string AssignedTo { get; set; } = string.Empty;
    public string? AssignedToName { get; set; }

    public TenderStatus Status { get; set; } = TenderStatus.Registered;
    public string? OutcomeNotes { get; set; }
    public string? LostReason { get; set; }
    public string? LinkedOpportunityId { get; set; }   // set when Won

    // Deadline alert guards (14/7/3/1 days before submission).
    public DateTime? Alert14SentAt { get; set; }
    public DateTime? Alert7SentAt { get; set; }
    public DateTime? Alert3SentAt { get; set; }
    public DateTime? Alert1SentAt { get; set; }

    public TenderBidBond? BidBond { get; set; }
}
