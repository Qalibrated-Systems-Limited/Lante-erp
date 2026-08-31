using Microsoft.EntityFrameworkCore;
using ComplianceService.Core.DTOs.Common;
using ComplianceService.Core.DTOs.Icm;
using ComplianceService.Core.Entities;
using ComplianceService.Core.Interfaces.Repositories;
using ComplianceService.Infrastructure.Data;

namespace ComplianceService.Infrastructure.Repositories;

public class IcsaRepository(ComplianceDbContext context)
    : GenericRepository<Icsa>(context), IIcsaRepository
{
    public async Task<PaginatedResult<Icsa>> GetPagedAsync(IcsaFilterParameters parameters)
    {
        var query = DbSet.AsQueryable();
        if (parameters.RelatedPartyId != null)
            query = query.Where(a => a.RelatedPartyId == parameters.RelatedPartyId);

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(a => a.StartDate)
            .Skip((parameters.Page - 1) * parameters.PageSize)
            .Take(parameters.PageSize)
            .ToListAsync();

        return new PaginatedResult<Icsa>
        {
            Items = items,
            TotalCount = totalCount,
            Page = parameters.Page,
            PageSize = parameters.PageSize
        };
    }
}
