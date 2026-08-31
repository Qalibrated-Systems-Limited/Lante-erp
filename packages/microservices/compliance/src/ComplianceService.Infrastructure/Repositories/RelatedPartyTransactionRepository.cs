using Microsoft.EntityFrameworkCore;
using ComplianceService.Core.DTOs.Common;
using ComplianceService.Core.DTOs.RelatedParty;
using ComplianceService.Core.Entities;
using ComplianceService.Core.Interfaces.Repositories;
using ComplianceService.Infrastructure.Data;

namespace ComplianceService.Infrastructure.Repositories;

public class RelatedPartyTransactionRepository(ComplianceDbContext context)
    : GenericRepository<RelatedPartyTransaction>(context), IRelatedPartyTransactionRepository
{
    public async Task<PaginatedResult<RelatedPartyTransaction>> GetPagedAsync(RelatedPartyTransactionFilterParameters parameters)
    {
        var query = DbSet.AsQueryable();
        if (parameters.UnreportedOnly == true)
            query = query.Where(t => !t.Reported);

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(t => t.TransactionDate)
            .Skip((parameters.Page - 1) * parameters.PageSize)
            .Take(parameters.PageSize)
            .ToListAsync();

        return new PaginatedResult<RelatedPartyTransaction>
        {
            Items = items,
            TotalCount = totalCount,
            Page = parameters.Page,
            PageSize = parameters.PageSize
        };
    }
}
