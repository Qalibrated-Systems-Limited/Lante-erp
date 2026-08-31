using ComplianceService.Core.DTOs.Common;
using ComplianceService.Core.DTOs.RelatedParty;
using ComplianceService.Core.Entities;

namespace ComplianceService.Core.Interfaces.Repositories;

public interface IRelatedPartyTransactionRepository : IGenericRepository<RelatedPartyTransaction>
{
    Task<PaginatedResult<RelatedPartyTransaction>> GetPagedAsync(RelatedPartyTransactionFilterParameters parameters);
}
