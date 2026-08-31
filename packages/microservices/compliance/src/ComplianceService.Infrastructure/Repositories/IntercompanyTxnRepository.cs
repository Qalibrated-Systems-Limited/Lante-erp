using Microsoft.EntityFrameworkCore;
using ComplianceService.Core.DTOs.Common;
using ComplianceService.Core.DTOs.Icm;
using ComplianceService.Core.Entities;
using ComplianceService.Core.Interfaces.Repositories;
using ComplianceService.Infrastructure.Data;

namespace ComplianceService.Infrastructure.Repositories;

public class IntercompanyTxnRepository(ComplianceDbContext context)
    : GenericRepository<IntercompanyTxn>(context), IIntercompanyTxnRepository
{
    public async Task<PaginatedResult<IntercompanyTxn>> GetPagedAsync(IntercompanyTxnFilterParameters parameters)
    {
        var query = DbSet.AsQueryable();
        if (parameters.UnreconciledOnly == true)
            query = query.Where(t => t.ReconciledAt == null);

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(t => t.PostedAt)
            .Skip((parameters.Page - 1) * parameters.PageSize)
            .Take(parameters.PageSize)
            .ToListAsync();

        return new PaginatedResult<IntercompanyTxn>
        {
            Items = items,
            TotalCount = totalCount,
            Page = parameters.Page,
            PageSize = parameters.PageSize
        };
    }
}
