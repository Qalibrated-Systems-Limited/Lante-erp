using Microsoft.EntityFrameworkCore;
using HSEService.Core.DTOs.Common;
using HSEService.Core.DTOs.Incidents;
using HSEService.Core.Entities;
using HSEService.Core.Enums;
using HSEService.Core.Interfaces.Repositories;
using HSEService.Infrastructure.Data;

namespace HSEService.Infrastructure.Repositories;

public class CorrectiveActionRepository(HSEDbContext context)
    : GenericRepository<CorrectiveAction>(context), ICorrectiveActionRepository
{
    public async Task<PaginatedResult<CorrectiveAction>> GetPagedAsync(CorrectiveActionFilterParameters parameters)
    {
        var query = DbSet.AsQueryable();

        if (!string.IsNullOrEmpty(parameters.IncidentId))
            query = query.Where(c => c.IncidentId == parameters.IncidentId);

        if (parameters.OpenOnly)
            query = query.Where(c => c.Status != CorrectiveActionStatus.Completed);

        var totalCount = await query.CountAsync();
        var items = await query
            .Skip((parameters.Page - 1) * parameters.PageSize)
            .Take(parameters.PageSize)
            .ToListAsync();

        return new PaginatedResult<CorrectiveAction>
        {
            Items = items,
            TotalCount = totalCount,
            Page = parameters.Page,
            PageSize = parameters.PageSize
        };
    }
}
