namespace SubcontractsService.Core.Entities;

// SUB-006: completed by Project Manager within 14 days of practical completion; scores
// auto-update the ASR record (Subcontractor.LatestPerformanceScore/WatchListed).
public class SubconScorecard : BaseEntity
{
    public string AwardId { get; set; } = string.Empty;
    public string? ProjectManagerUserId { get; set; }
    public string? ProjectManagerName { get; set; }
    public decimal Score { get; set; }
    public DateTime CompletedOn { get; set; }
    public string? Notes { get; set; }
}
