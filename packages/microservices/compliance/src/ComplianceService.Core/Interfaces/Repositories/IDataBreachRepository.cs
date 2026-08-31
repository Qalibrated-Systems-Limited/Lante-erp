using ComplianceService.Core.DTOs.Common;
using ComplianceService.Core.Entities;

namespace ComplianceService.Core.Interfaces.Repositories;

public interface IDataBreachRepository : IGenericRepository<DataBreach>
{
    new Task<PaginatedResult<DataBreach>> GetPagedAsync(PaginationParameters parameters);
}
