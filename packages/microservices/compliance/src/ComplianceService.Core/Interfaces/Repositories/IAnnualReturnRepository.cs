using ComplianceService.Core.DTOs.Common;
using ComplianceService.Core.Entities;

namespace ComplianceService.Core.Interfaces.Repositories;

public interface IAnnualReturnRepository : IGenericRepository<AnnualReturn>
{
    new Task<PaginatedResult<AnnualReturn>> GetPagedAsync(PaginationParameters parameters);
}
