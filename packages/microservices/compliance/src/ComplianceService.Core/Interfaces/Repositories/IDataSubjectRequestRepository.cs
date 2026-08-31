using ComplianceService.Core.DTOs.Common;
using ComplianceService.Core.Entities;

namespace ComplianceService.Core.Interfaces.Repositories;

public interface IDataSubjectRequestRepository : IGenericRepository<DataSubjectRequest>
{
    new Task<PaginatedResult<DataSubjectRequest>> GetPagedAsync(PaginationParameters parameters);
}
