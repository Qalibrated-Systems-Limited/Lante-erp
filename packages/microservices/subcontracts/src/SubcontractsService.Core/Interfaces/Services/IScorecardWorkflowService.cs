using SubcontractsService.Core.Entities;

namespace SubcontractsService.Core.Interfaces.Services;

public interface IScorecardWorkflowService
{
    // SUB-006: recording a scorecard auto-updates the Subcontractor's LatestPerformanceScore,
    // and flags WatchListed = true when the score is below 6.0.
    Task<SubconScorecard> RecordAsync(SubconScorecard scorecard, string userId);
}
