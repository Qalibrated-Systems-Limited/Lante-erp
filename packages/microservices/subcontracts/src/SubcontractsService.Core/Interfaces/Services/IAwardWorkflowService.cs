using SubcontractsService.Core.Entities;

namespace SubcontractsService.Core.Interfaces.Services;

public interface IAwardWorkflowService
{
    // SUB-005: checks HSE for an approved RAMS record before flipping to Mobilized; throws
    // InvalidOperationException if not approved, or if the subcontractor is IsRestricted (SUB-008).
    Task<SubcontractAward> ActivateMobilizationAsync(string tenantSchema, string awardId, string userId);
}
