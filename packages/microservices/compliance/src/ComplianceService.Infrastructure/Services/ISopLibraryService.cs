using ComplianceService.Core.DTOs.SopLibrary;
using ComplianceService.Core.Entities;

namespace ComplianceService.Infrastructure.Services;

// Lives in Infrastructure (not Core) — unlike the generic IComplianceCrudService<T>, Create here
// needs a real DbContext-level transaction to generate the Code atomically, so it can't be backed
// by the generic repository alone. Mirrors ITenantProvisioningService's placement for the same
// reason (needs direct DbContext access).
public interface ISopLibraryService
{
    Task<SopDocument> CreateAsync(CreateSopDto dto, string createdBy);
}
