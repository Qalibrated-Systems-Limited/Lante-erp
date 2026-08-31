using Microsoft.EntityFrameworkCore;
using HSEService.Core.DTOs.Common;
using HSEService.Core.DTOs.Training;
using HSEService.Core.Entities;
using HSEService.Core.Interfaces.Repositories;
using HSEService.Infrastructure.Data;

namespace HSEService.Infrastructure.Repositories;

public class HseTrainingRecordRepository(HSEDbContext context)
    : GenericRepository<HseTrainingRecord>(context), IHseTrainingRecordRepository
{
    public async Task<PaginatedResult<HseTrainingRecord>> GetPagedAsync(HseTrainingRecordFilterParameters parameters)
    {
        var query = DbSet.AsQueryable();

        if (!string.IsNullOrEmpty(parameters.EmployeeUserId))
            query = query.Where(t => t.EmployeeUserId == parameters.EmployeeUserId);

        var totalCount = await query.CountAsync();
        var items = await query
            .Skip((parameters.Page - 1) * parameters.PageSize)
            .Take(parameters.PageSize)
            .ToListAsync();

        return new PaginatedResult<HseTrainingRecord>
        {
            Items = items,
            TotalCount = totalCount,
            Page = parameters.Page,
            PageSize = parameters.PageSize
        };
    }
}
