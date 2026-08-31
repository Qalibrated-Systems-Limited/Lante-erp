using Microsoft.EntityFrameworkCore;
using ComplianceService.Core.DTOs.Common;
using ComplianceService.Core.DTOs.Training;
using ComplianceService.Core.Entities;
using ComplianceService.Core.Interfaces.Repositories;
using ComplianceService.Infrastructure.Data;

namespace ComplianceService.Infrastructure.Repositories;

public class AntiBriberyTrainingRepository(ComplianceDbContext context)
    : GenericRepository<AntiBriberyTraining>(context), IAntiBriberyTrainingRepository
{
    public async Task<PaginatedResult<AntiBriberyTraining>> GetPagedAsync(AntiBriberyTrainingFilterParameters parameters)
    {
        var query = DbSet.AsQueryable();
        if (parameters.EmployeeUserId != null)
            query = query.Where(t => t.EmployeeUserId == parameters.EmployeeUserId);

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(t => t.CompletedOn)
            .Skip((parameters.Page - 1) * parameters.PageSize)
            .Take(parameters.PageSize)
            .ToListAsync();

        return new PaginatedResult<AntiBriberyTraining>
        {
            Items = items,
            TotalCount = totalCount,
            Page = parameters.Page,
            PageSize = parameters.PageSize
        };
    }
}
