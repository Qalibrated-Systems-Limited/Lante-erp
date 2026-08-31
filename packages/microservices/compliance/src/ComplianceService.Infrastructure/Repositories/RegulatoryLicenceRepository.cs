using Microsoft.EntityFrameworkCore;
using ComplianceService.Core.DTOs.Common;
using ComplianceService.Core.DTOs.Licences;
using ComplianceService.Core.Entities;
using ComplianceService.Core.Interfaces.Repositories;
using ComplianceService.Infrastructure.Data;

namespace ComplianceService.Infrastructure.Repositories;

public class RegulatoryLicenceRepository(ComplianceDbContext context)
    : GenericRepository<RegulatoryLicence>(context), IRegulatoryLicenceRepository
{
    public async Task<PaginatedResult<RegulatoryLicence>> GetPagedAsync(RegulatoryLicenceFilterParameters parameters)
    {
        var query = DbSet.AsQueryable();
        if (parameters.Type.HasValue)
            query = query.Where(l => l.Type == parameters.Type.Value);

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderBy(l => l.ExpiryDate)
            .Skip((parameters.Page - 1) * parameters.PageSize)
            .Take(parameters.PageSize)
            .ToListAsync();

        return new PaginatedResult<RegulatoryLicence>
        {
            Items = items,
            TotalCount = totalCount,
            Page = parameters.Page,
            PageSize = parameters.PageSize
        };
    }
}
