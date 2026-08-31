using Microsoft.EntityFrameworkCore;
using ComplianceService.Core.DTOs.Common;
using ComplianceService.Core.Entities;
using ComplianceService.Core.Interfaces.Repositories;
using ComplianceService.Infrastructure.Data;

namespace ComplianceService.Infrastructure.Repositories;

public class TaxComplianceCertRepository(ComplianceDbContext context)
    : GenericRepository<TaxComplianceCert>(context), ITaxComplianceCertRepository
{
    public new async Task<PaginatedResult<TaxComplianceCert>> GetPagedAsync(PaginationParameters parameters)
    {
        var query = DbSet.AsQueryable();
        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(t => t.ExpiryDate)
            .Skip((parameters.Page - 1) * parameters.PageSize)
            .Take(parameters.PageSize)
            .ToListAsync();

        return new PaginatedResult<TaxComplianceCert>
        {
            Items = items,
            TotalCount = totalCount,
            Page = parameters.Page,
            PageSize = parameters.PageSize
        };
    }
}
