using SubcontractsService.Core.Entities;
using SubcontractsService.Core.Enums;
using SubcontractsService.Core.Interfaces.Repositories;
using SubcontractsService.Core.Interfaces.Services;

namespace SubcontractsService.Core.Services;

public class PrequalificationWorkflowService : IPrequalificationWorkflowService
{
    private readonly IGenericRepository<Prequalification> _pqqRepository;
    private readonly IGenericRepository<Subcontractor> _subcontractorRepository;

    public PrequalificationWorkflowService(
        IGenericRepository<Prequalification> pqqRepository,
        IGenericRepository<Subcontractor> subcontractorRepository)
    {
        _pqqRepository = pqqRepository;
        _subcontractorRepository = subcontractorRepository;
    }

    public async Task<Prequalification> ApproveAsync(string prequalificationId, decimal score, string approvedByUserId, string approvedByName)
    {
        var pqq = await _pqqRepository.GetByIdAsync(prequalificationId)
            ?? throw new InvalidOperationException("Prequalification not found.");

        if (pqq.Status is not (PrequalificationStatus.Sent or PrequalificationStatus.Completed))
            throw new InvalidOperationException(
                $"Prequalification is already {pqq.Status} and cannot be approved again.");

        pqq.Status = PrequalificationStatus.Approved;
        pqq.Score = score;
        pqq.ApprovedByUserId = approvedByUserId;
        pqq.ApprovedByName = approvedByName;
        pqq.ApprovedOn = DateTime.UtcNow;
        await _pqqRepository.UpdateAsync(pqq);

        var subcontractor = await _subcontractorRepository.GetByIdAsync(pqq.SubcontractorId);
        if (subcontractor != null)
        {
            subcontractor.PqqScore = score;
            subcontractor.Prequalified = true;
            await _subcontractorRepository.UpdateAsync(subcontractor);
        }

        return pqq;
    }

    public async Task<Prequalification> RejectAsync(string prequalificationId, string approvedByUserId, string approvedByName)
    {
        var pqq = await _pqqRepository.GetByIdAsync(prequalificationId)
            ?? throw new InvalidOperationException("Prequalification not found.");

        if (pqq.Status is not (PrequalificationStatus.Sent or PrequalificationStatus.Completed))
            throw new InvalidOperationException(
                $"Prequalification is already {pqq.Status} and cannot be rejected.");

        pqq.Status = PrequalificationStatus.Rejected;
        pqq.ApprovedByUserId = approvedByUserId;
        pqq.ApprovedByName = approvedByName;
        pqq.ApprovedOn = DateTime.UtcNow;
        await _pqqRepository.UpdateAsync(pqq);
        return pqq;
    }
}
