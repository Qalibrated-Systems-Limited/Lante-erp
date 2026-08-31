namespace CrmService.Core.Entities;

/// <summary>
/// P3 — OPPORTUNITY_COMPETITOR (CRM-014). Competitors on an opportunity; WasSelected marks the one
/// that won a lost deal. The aggregate of these rows is the competitor-intelligence database
/// (competitor stored as a name — no separate master needed to derive intelligence).
/// </summary>
public class OpportunityCompetitor : BaseEntity
{
    public string OpportunityId { get; set; } = string.Empty;
    public string CompetitorName { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public bool WasSelected { get; set; }   // this competitor won the deal

    public Opportunity? Opportunity { get; set; }
}
