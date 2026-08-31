using Microsoft.EntityFrameworkCore;
using ComplianceService.Core.DTOs.Coi;
using ComplianceService.Core.DTOs.Common;
using ComplianceService.Core.Entities;
using ComplianceService.Core.Interfaces.Repositories;
using ComplianceService.Infrastructure.Data;

namespace ComplianceService.Infrastructure.Repositories;

public class CoiDeclarationRepository(ComplianceDbContext context)
    : GenericRepository<CoiDeclaration>(context), ICoiDeclarationRepository
{
    public async Task<PaginatedResult<CoiDeclaration>> GetPagedAsync(CoiDeclarationFilterParameters parameters)
    {
        var query = DbSet.AsQueryable();
        if (parameters.Year.HasValue)
            query = query.Where(c => c.Year == parameters.Year.Value);

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip((parameters.Page - 1) * parameters.PageSize)
            .Take(parameters.PageSize)
            .ToListAsync();

        return new PaginatedResult<CoiDeclaration>
        {
            Items = items,
            TotalCount = totalCount,
            Page = parameters.Page,
            PageSize = parameters.PageSize
        };
    }
}
