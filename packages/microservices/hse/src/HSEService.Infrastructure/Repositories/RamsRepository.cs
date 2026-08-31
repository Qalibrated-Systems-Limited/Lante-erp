using Microsoft.EntityFrameworkCore;
using HSEService.Core.DTOs.Common;
using HSEService.Core.DTOs.Rams;
using HSEService.Core.Entities;
using HSEService.Core.Interfaces.Repositories;
using HSEService.Infrastructure.Data;

namespace HSEService.Infrastructure.Repositories;

public class RamsRepository(HSEDbContext context)
    : GenericRepository<Rams>(context), IRamsRepository
{
    public async Task<PaginatedResult<Rams>> GetPagedAsync(RamsFilterParameters parameters)
    {
        var query = DbSet.AsQueryable();

        if (!string.IsNullOrEmpty(parameters.SiteId))
            query = query.Where(r => r.SiteId == parameters.SiteId);

        query = query.OrderByDescending(r => r.CreatedAt);

        var totalCount = await query.CountAsync();
        var items = await query
            .Skip((parameters.Page - 1) * parameters.PageSize)
            .Take(parameters.PageSize)
            .ToListAsync();

        return new PaginatedResult<Rams>
        {
            Items = items,
            TotalCount = totalCount,
            Page = parameters.Page,
            PageSize = parameters.PageSize
        };
    }
}
