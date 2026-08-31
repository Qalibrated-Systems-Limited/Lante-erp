using CrmService.Core.Enums;

namespace CrmService.Core.Entities;

/// <summary>
/// P3 — Opportunity management (CRM-002/003/009–015). A weighted sales pipeline: every opportunity
/// sits in a PipelineStage carrying a win probability; weighted value = EstimatedValue × Probability.
/// A daily sweep escalates opportunities with no stage movement in 14 days. Lost requires a reason +
/// winning competitor; Won triggers deal close (P6/C5). Created standalone or from a converted lead.
/// </summary>
public class Opportunity : BaseEntity
{
    public string OpportunityNumber { get; set; } = string.Empty;   // OPP-{year}-{seq}
    public string Name { get; set; } = string.Empty;

    public string? CustomerId { get; set; }     // CRM customer (may be null until onboarded)
    public string? CustomerName { get; set; }
    public string? LeadId { get; set; }          // source lead if converted from one

    public string PipelineStageId { get; set; } = string.Empty;
    public string StageName { get; set; } = string.Empty;   // denormalised for fast reads
    public decimal Probability { get; set; }                // fraction 0..1, set from the stage

    public string AssignedTo { get; set; } = string.Empty;   // Sales Engineer
    public string? AssignedToName { get; set; }

    public decimal EstimatedValue { get; set; }
    public string? Source { get; set; }
    public DateTime? ExpectedCloseDate { get; set; }
    public DateTime? ActualCloseDate { get; set; }

    public OpportunityStatus Status { get; set; } = OpportunityStatus.Open;
    public string? LostReason { get; set; }

    public DateTime StageMovedAt { get; set; } = DateTime.UtcNow;   // drives 14-day stale sweep
    public DateTime? StaleAlertedAt { get; set; }

    public string? DealId { get; set; }   // set when Won → deal close (C5)

    public ICollection<OpportunityActivity> Activities { get; set; } = new List<OpportunityActivity>();
    public ICollection<OpportunityCompetitor> Competitors { get; set; } = new List<OpportunityCompetitor>();
}
