namespace SubcontractsService.Core.Interfaces.Services;

// SUB-005 hard gate: mobilization cannot be activated until HSE confirms this subcontractor has
// an approved RAMS record for the site. Backed by HSE's internal RAMS-status endpoint.
public interface IHseServiceClient
{
    Task<bool> IsRamsApprovedAsync(string tenantSchema, string subcontractorId, string? siteId);
}
