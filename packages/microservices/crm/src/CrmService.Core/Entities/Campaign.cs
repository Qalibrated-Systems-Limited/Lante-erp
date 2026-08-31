using CrmService.Core.Enums;

namespace CrmService.Core.Entities;

/// <summary>P11 — CAMPAIGN (+ MARKETING_BUDGET folded in). Registered before launch with type, target
/// audience, dates &amp; approved budget; an alert fires when 80% of budget is consumed. Leads are tagged
/// via Lead.CampaignId (CAMPAIGN_LEAD folded there); ROI = attributed revenue / actual spend.</summary>
public class Campaign : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public CampaignType CampaignType { get; set; } = CampaignType.Digital;
    public string? Description { get; set; }
    public string? TargetAudience { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public CampaignStatus Status { get; set; } = CampaignStatus.Planned;
    public decimal Budget { get; set; }
    public decimal ActualSpend { get; set; }
    public DateTime? Alert80SentAt { get; set; }
}
