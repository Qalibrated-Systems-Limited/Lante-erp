using SubcontractsService.Core.Entities;

namespace SubcontractsService.Core.Interfaces.Services;

public interface IPrequalificationWorkflowService
{
    // SUB-002: approving a PQQ automatically updates the linked Subcontractor's ASR fields
    // (PqqScore, Prequalified = true).
    Task<Prequalification> ApproveAsync(string prequalificationId, decimal score, string approvedByUserId, string approvedByName);
    Task<Prequalification> RejectAsync(string prequalificationId, string approvedByUserId, string approvedByName);
}
