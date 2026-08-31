using Microsoft.EntityFrameworkCore;
using ComplianceService.Core.DTOs.Common;
using ComplianceService.Core.Entities;
using ComplianceService.Core.Interfaces.Repositories;
using ComplianceService.Infrastructure.Data;

namespace ComplianceService.Infrastructure.Repositories;

// Hides GenericRepository<T>'s unordered GetPagedAsync to preserve GetAll's existing
// "most recently received first" ordering under pagination.
public class DataSubjectRequestRepository(ComplianceDbContext context)
    : GenericRepository<DataSubjectRequest>(context), IDataSubjectRequestRepository
{
    public new async Task<PaginatedResult<DataSubjectRequest>> GetPagedAsync(PaginationParameters parameters)
    {
        var query = DbSet.AsQueryable();
        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(r => r.ReceivedOn)
            .Skip((parameters.Page - 1) * parameters.PageSize)
            .Take(parameters.PageSize)
            .ToListAsync();

        return new PaginatedResult<DataSubjectRequest>
        {
            Items = items,
            TotalCount = totalCount,
            Page = parameters.Page,
            PageSize = parameters.PageSize
        };
    }
}
