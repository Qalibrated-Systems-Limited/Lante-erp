using ComplianceService.Core.DTOs.Common;
using ComplianceService.Core.Entities;

namespace ComplianceService.Core.Interfaces.Repositories;

public interface IRelatedPartyRepository : IGenericRepository<RelatedParty>
{
    new Task<PaginatedResult<RelatedParty>> GetPagedAsync(PaginationParameters parameters);
}
