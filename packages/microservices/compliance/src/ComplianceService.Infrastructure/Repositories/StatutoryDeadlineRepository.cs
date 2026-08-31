using Microsoft.EntityFrameworkCore;
using ComplianceService.Core.DTOs.Common;
using ComplianceService.Core.DTOs.Statutory;
using ComplianceService.Core.Entities;
using ComplianceService.Core.Interfaces.Repositories;
using ComplianceService.Infrastructure.Data;

namespace ComplianceService.Infrastructure.Repositories;

public class StatutoryDeadlineRepository(ComplianceDbContext context)
    : GenericRepository<StatutoryDeadline>(context), IStatutoryDeadlineRepository
{
    public async Task<PaginatedResult<StatutoryDeadline>> GetPagedAsync(StatutoryDeadlineFilterParameters parameters)
    {
        var query = DbSet.AsQueryable();
        if (parameters.ObligationId != null)
            query = query.Where(d => d.ObligationId == parameters.ObligationId);

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderBy(d => d.DueDate)
            .Skip((parameters.Page - 1) * parameters.PageSize)
            .Take(parameters.PageSize)
            .ToListAsync();

        return new PaginatedResult<StatutoryDeadline>
        {
            Items = items,
            TotalCount = totalCount,
            Page = parameters.Page,
            PageSize = parameters.PageSize
        };
    }
}
