using ComplianceService.Core.DTOs.Common;
using ComplianceService.Core.DTOs.Icm;
using ComplianceService.Core.Entities;

namespace ComplianceService.Core.Interfaces.Repositories;

public interface IIcsaRepository : IGenericRepository<Icsa>
{
    Task<PaginatedResult<Icsa>> GetPagedAsync(IcsaFilterParameters parameters);
}
