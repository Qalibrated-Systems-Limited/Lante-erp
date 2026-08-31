namespace SubcontractsService.Core.DTOs.Scorecards;

// SUB-006: post-completion performance scorecard.
public class SubconScorecardReadDto
{
    public string Id { get; set; } = string.Empty;
    public string AwardId { get; set; } = string.Empty;
    public string? ProjectManagerUserId { get; set; }
    public string? ProjectManagerName { get; set; }
    public decimal Score { get; set; }
    public DateTime CompletedOn { get; set; }
    public string? Notes { get; set; }
}

public class CreateSubconScorecardDto
{
    public string AwardId { get; set; } = string.Empty;
    public string? ProjectManagerName { get; set; }
    public decimal Score { get; set; }
    public DateTime CompletedOn { get; set; }
    public string? Notes { get; set; }
}

// Core-field edit — AwardId/ProjectManagerUserId are fixed after creation (identity fields).
public class UpdateSubconScorecardDto
{
    public string? ProjectManagerName { get; set; }
    public decimal Score { get; set; }
    public DateTime CompletedOn { get; set; }
    public string? Notes { get; set; }
}
