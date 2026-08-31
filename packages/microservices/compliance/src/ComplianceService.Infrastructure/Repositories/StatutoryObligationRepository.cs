using Microsoft.EntityFrameworkCore;
using ComplianceService.Core.DTOs.Common;
using ComplianceService.Core.Entities;
using ComplianceService.Core.Interfaces.Repositories;
using ComplianceService.Infrastructure.Data;

namespace ComplianceService.Infrastructure.Repositories;

// Hides GenericRepository<T>'s GetPagedAsync so GetAll's fixed "active only" business
// rule survives under pagination — this is not a user-controlled filter.
public class StatutoryObligationRepository(ComplianceDbContext context)
    : GenericRepository<StatutoryObligation>(context), IStatutoryObligationRepository
{
    public new async Task<PaginatedResult<StatutoryObligation>> GetPagedAsync(PaginationParameters parameters)
    {
        var query = DbSet.Where(o => o.IsActive);
        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(o => o.CreatedAt)
            .Skip((parameters.Page - 1) * parameters.PageSize)
            .Take(parameters.PageSize)
            .ToListAsync();

        return new PaginatedResult<StatutoryObligation>
        {
            Items = items,
            TotalCount = totalCount,
            Page = parameters.Page,
            PageSize = parameters.PageSize
        };
    }
}
