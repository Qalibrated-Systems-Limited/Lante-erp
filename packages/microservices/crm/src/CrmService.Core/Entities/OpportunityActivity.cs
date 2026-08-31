namespace CrmService.Core.Entities;

/// <summary>P3 — OPPORTUNITY_ACTIVITY. Touchpoints & scheduled follow-ups against an opportunity.</summary>
public class OpportunityActivity : BaseEntity
{
    public string OpportunityId { get; set; } = string.Empty;
    public string ActivityType { get; set; } = string.Empty;   // Call / Email / Meeting / Note
    public string Subject { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Outcome { get; set; }
    public string PerformedBy { get; set; } = string.Empty;
    public DateTime ActivityDate { get; set; } = DateTime.UtcNow;
    public DateTime? NextFollowUp { get; set; }

    public Opportunity? Opportunity { get; set; }
}
