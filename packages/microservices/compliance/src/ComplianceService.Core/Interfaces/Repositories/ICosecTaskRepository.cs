using ComplianceService.Core.DTOs.Common;
using ComplianceService.Core.Entities;

namespace ComplianceService.Core.Interfaces.Repositories;

public interface ICosecTaskRepository : IGenericRepository<CosecTask>
{
    new Task<PaginatedResult<CosecTask>> GetPagedAsync(PaginationParameters parameters);
}
