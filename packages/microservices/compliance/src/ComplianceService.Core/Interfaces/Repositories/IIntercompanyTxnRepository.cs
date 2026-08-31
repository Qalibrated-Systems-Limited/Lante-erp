using ComplianceService.Core.DTOs.Common;
using ComplianceService.Core.DTOs.Icm;
using ComplianceService.Core.Entities;

namespace ComplianceService.Core.Interfaces.Repositories;

public interface IIntercompanyTxnRepository : IGenericRepository<IntercompanyTxn>
{
    Task<PaginatedResult<IntercompanyTxn>> GetPagedAsync(IntercompanyTxnFilterParameters parameters);
}
