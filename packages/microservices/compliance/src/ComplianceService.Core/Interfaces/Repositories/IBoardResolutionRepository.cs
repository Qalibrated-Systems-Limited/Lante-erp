using ComplianceService.Core.DTOs.Common;
using ComplianceService.Core.Entities;

namespace ComplianceService.Core.Interfaces.Repositories;

public interface IBoardResolutionRepository : IGenericRepository<BoardResolution>
{
    new Task<PaginatedResult<BoardResolution>> GetPagedAsync(PaginationParameters parameters);
}
