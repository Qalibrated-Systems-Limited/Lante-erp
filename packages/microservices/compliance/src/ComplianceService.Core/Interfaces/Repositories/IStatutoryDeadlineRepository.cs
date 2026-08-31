using ComplianceService.Core.DTOs.Common;
using ComplianceService.Core.DTOs.Statutory;
using ComplianceService.Core.Entities;

namespace ComplianceService.Core.Interfaces.Repositories;

public interface IStatutoryDeadlineRepository : IGenericRepository<StatutoryDeadline>
{
    Task<PaginatedResult<StatutoryDeadline>> GetPagedAsync(StatutoryDeadlineFilterParameters parameters);
}
