using SubcontractsService.Core.Entities;
using SubcontractsService.Core.Interfaces.Repositories;
using SubcontractsService.Core.Interfaces.Services;

namespace SubcontractsService.Core.Services;

public class ScorecardWorkflowService : IScorecardWorkflowService
{
    private const decimal WatchListThreshold = 6.0m;

    private readonly IGenericRepository<SubconScorecard> _scorecardRepository;
    private readonly IGenericRepository<SubcontractAward> _awardRepository;
    private readonly IGenericRepository<Subcontractor> _subcontractorRepository;

    public ScorecardWorkflowService(
        IGenericRepository<SubconScorecard> scorecardRepository,
        IGenericRepository<SubcontractAward> awardRepository,
        IGenericRepository<Subcontractor> subcontractorRepository)
    {
        _scorecardRepository = scorecardRepository;
        _awardRepository = awardRepository;
        _subcontractorRepository = subcontractorRepository;
    }

    public async Task<SubconScorecard> RecordAsync(SubconScorecard scorecard, string userId)
    {
        scorecard.CreatedBy = userId;
        await _scorecardRepository.CreateAsync(scorecard);

        var award = await _awardRepository.GetByIdAsync(scorecard.AwardId);
        if (award != null)
        {
            var subcontractor = await _subcontractorRepository.GetByIdAsync(award.SubcontractorId);
            if (subcontractor != null)
            {
                subcontractor.LatestPerformanceScore = scorecard.Score;
                subcontractor.WatchListed = scorecard.Score < WatchListThreshold;
                await _subcontractorRepository.UpdateAsync(subcontractor);
            }
        }

        return scorecard;
    }
}
