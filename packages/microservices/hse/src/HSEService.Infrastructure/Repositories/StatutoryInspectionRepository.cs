using Microsoft.EntityFrameworkCore;
using HSEService.Core.DTOs.Common;
using HSEService.Core.DTOs.Inspections;
using HSEService.Core.Entities;
using HSEService.Core.Interfaces.Repositories;
using HSEService.Infrastructure.Data;

namespace HSEService.Infrastructure.Repositories;

public class StatutoryInspectionRepository(HSEDbContext context)
    : GenericRepository<StatutoryInspection>(context), IStatutoryInspectionRepository
{
    public async Task<PaginatedResult<StatutoryInspection>> GetPagedAsync(StatutoryInspectionFilterParameters parameters)
    {
        var query = DbSet.AsQueryable();

        if (!string.IsNullOrEmpty(parameters.SiteId))
            query = query.Where(i => i.SiteId == parameters.SiteId);

        query = query.OrderByDescending(i => i.CreatedAt);

        var totalCount = await query.CountAsync();
        var items = await query
            .Skip((parameters.Page - 1) * parameters.PageSize)
            .Take(parameters.PageSize)
            .ToListAsync();

        return new PaginatedResult<StatutoryInspection>
        {
            Items = items,
            TotalCount = totalCount,
            Page = parameters.Page,
            PageSize = parameters.PageSize
        };
    }
}
