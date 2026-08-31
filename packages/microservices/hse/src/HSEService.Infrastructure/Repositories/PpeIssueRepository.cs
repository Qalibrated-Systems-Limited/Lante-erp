using Microsoft.EntityFrameworkCore;
using HSEService.Core.DTOs.Common;
using HSEService.Core.DTOs.Ppe;
using HSEService.Core.Entities;
using HSEService.Core.Interfaces.Repositories;
using HSEService.Infrastructure.Data;

namespace HSEService.Infrastructure.Repositories;

public class PpeIssueRepository(HSEDbContext context)
    : GenericRepository<PpeIssue>(context), IPpeIssueRepository
{
    public async Task<PaginatedResult<PpeIssue>> GetPagedAsync(PpeIssueFilterParameters parameters)
    {
        var query = DbSet.AsQueryable();

        if (!string.IsNullOrEmpty(parameters.EmployeeUserId))
            query = query.Where(p => p.EmployeeUserId == parameters.EmployeeUserId);

        var totalCount = await query.CountAsync();
        var items = await query
            .Skip((parameters.Page - 1) * parameters.PageSize)
            .Take(parameters.PageSize)
            .ToListAsync();

        return new PaginatedResult<PpeIssue>
        {
            Items = items,
            TotalCount = totalCount,
            Page = parameters.Page,
            PageSize = parameters.PageSize
        };
    }
}
