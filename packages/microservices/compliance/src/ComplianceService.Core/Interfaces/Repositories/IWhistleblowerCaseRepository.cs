using ComplianceService.Core.DTOs.Common;
using ComplianceService.Core.Entities;

namespace ComplianceService.Core.Interfaces.Repositories;

public interface IWhistleblowerCaseRepository : IGenericRepository<WhistleblowerCase>
{
    new Task<PaginatedResult<WhistleblowerCase>> GetPagedAsync(PaginationParameters parameters);
}
