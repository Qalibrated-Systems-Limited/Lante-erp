using Microsoft.EntityFrameworkCore;
using ComplianceService.Core.DTOs.Common;
using ComplianceService.Core.Entities;
using ComplianceService.Core.Interfaces.Repositories;
using ComplianceService.Infrastructure.Data;

namespace ComplianceService.Infrastructure.Repositories;

public class DataBreachRepository(ComplianceDbContext context)
    : GenericRepository<DataBreach>(context), IDataBreachRepository
{
    public new async Task<PaginatedResult<DataBreach>> GetPagedAsync(PaginationParameters parameters)
    {
        var query = DbSet.AsQueryable();
        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(b => b.DiscoveredAt)
            .Skip((parameters.Page - 1) * parameters.PageSize)
            .Take(parameters.PageSize)
            .ToListAsync();

        return new PaginatedResult<DataBreach>
        {
            Items = items,
            TotalCount = totalCount,
            Page = parameters.Page,
            PageSize = parameters.PageSize
        };
    }
}
