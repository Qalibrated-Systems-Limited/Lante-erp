using Microsoft.EntityFrameworkCore;
using ComplianceService.Core.DTOs.Common;
using ComplianceService.Core.DTOs.Gifts;
using ComplianceService.Core.Entities;
using ComplianceService.Core.Interfaces.Repositories;
using ComplianceService.Infrastructure.Data;

namespace ComplianceService.Infrastructure.Repositories;

public class GiftHospitalityRepository(ComplianceDbContext context)
    : GenericRepository<GiftHospitality>(context), IGiftHospitalityRepository
{
    public async Task<PaginatedResult<GiftHospitality>> GetPagedAsync(GiftHospitalityFilterParameters parameters)
    {
        var query = DbSet.AsQueryable();
        if (parameters.FlaggedOnly == true)
            query = query.Where(g => g.Flagged);

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(g => g.Date)
            .Skip((parameters.Page - 1) * parameters.PageSize)
            .Take(parameters.PageSize)
            .ToListAsync();

        return new PaginatedResult<GiftHospitality>
        {
            Items = items,
            TotalCount = totalCount,
            Page = parameters.Page,
            PageSize = parameters.PageSize
        };
    }
}
