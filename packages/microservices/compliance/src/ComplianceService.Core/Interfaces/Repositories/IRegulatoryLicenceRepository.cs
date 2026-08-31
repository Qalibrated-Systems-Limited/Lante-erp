using ComplianceService.Core.DTOs.Common;
using ComplianceService.Core.DTOs.Licences;
using ComplianceService.Core.Entities;

namespace ComplianceService.Core.Interfaces.Repositories;

public interface IRegulatoryLicenceRepository : IGenericRepository<RegulatoryLicence>
{
    Task<PaginatedResult<RegulatoryLicence>> GetPagedAsync(RegulatoryLicenceFilterParameters parameters);
}
