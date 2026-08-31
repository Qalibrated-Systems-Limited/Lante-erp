using SubcontractsService.Core.Entities;
using SubcontractsService.Core.Enums;
using SubcontractsService.Core.Interfaces.Repositories;
using SubcontractsService.Core.Interfaces.Services;

namespace SubcontractsService.Core.Services;

public class AwardWorkflowService : IAwardWorkflowService
{
    private readonly IGenericRepository<SubcontractAward> _awardRepository;
    private readonly IGenericRepository<Subcontractor> _subcontractorRepository;
    private readonly IHseServiceClient _hseServiceClient;

    public AwardWorkflowService(
        IGenericRepository<SubcontractAward> awardRepository,
        IGenericRepository<Subcontractor> subcontractorRepository,
        IHseServiceClient hseServiceClient)
    {
        _awardRepository = awardRepository;
        _subcontractorRepository = subcontractorRepository;
        _hseServiceClient = hseServiceClient;
    }

    public async Task<SubcontractAward> ActivateMobilizationAsync(string tenantSchema, string awardId, string userId)
    {
        var award = await _awardRepository.GetByIdAsync(awardId)
            ?? throw new InvalidOperationException("Award not found.");

        if (award.Status != AwardStatus.Approved)
            throw new InvalidOperationException("Award must be Approved before mobilization can be activated.");

        var subcontractor = await _subcontractorRepository.GetByIdAsync(award.SubcontractorId);
        if (subcontractor?.IsRestricted == true)
            throw new InvalidOperationException("Subcontractor is restricted (expired insurance/TCC) and cannot be mobilized.");

        var ramsApproved = await _hseServiceClient.IsRamsApprovedAsync(tenantSchema, award.SubcontractorId, award.ProjectId);
        if (!ramsApproved)
            throw new InvalidOperationException("RAMS has not been uploaded and approved for this subcontractor. Mobilization cannot proceed.");

        award.RamsApproved = true;
        award.Status = AwardStatus.Mobilized;
        award.MobilizationActivatedAt = DateTime.UtcNow;
        award.UpdatedBy = userId;
        await _awardRepository.UpdateAsync(award);
        return award;
    }
}
