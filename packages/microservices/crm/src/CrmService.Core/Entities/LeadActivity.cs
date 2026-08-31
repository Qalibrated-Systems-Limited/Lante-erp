namespace CrmService.Core.Entities;

/// <summary>P2 — LEAD_ACTIVITY. Every touchpoint against a lead, chronologically. Logging an
/// activity refreshes the parent lead's LastActivityAt (clears the 2-day stale flag).</summary>
public class LeadActivity : BaseEntity
{
    public string LeadId { get; set; } = string.Empty;
    public string ActivityType { get; set; } = string.Empty;   // Call / Email / Meeting / Note
    public string Subject { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string PerformedBy { get; set; } = string.Empty;
    public DateTime ActivityDate { get; set; } = DateTime.UtcNow;

    public Lead? Lead { get; set; }
}
