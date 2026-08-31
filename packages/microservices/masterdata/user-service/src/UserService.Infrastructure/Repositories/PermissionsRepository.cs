using Microsoft.EntityFrameworkCore;
using UserService.Core.DTOs.Common;
using UserService.Core.Entities;
using UserService.Core.Interfaces.Repositories;
using UserService.Infrastructure.Data;

namespace UserService.Infrastructure.Repositories;

public class PermissionsRepository(LanteUserServiceDbContext context)
    : GenericRepository<Permission>(context), IPermissionsRepository
{
    public async Task<Permission?> GetByNameAsync(string name)
    {
        return await Context.Permissions.FirstOrDefaultAsync(p => p.Name == name && !p.IsDeleted);
    }

    public new async Task<PaginatedResult<Permission>> GetPagedAsync(PaginationParameters parameters)
    {
        var query = Context.Permissions.AsQueryable();

        if (!string.IsNullOrWhiteSpace(parameters.Search))
        {
            var search = parameters.Search.ToLower();
            query = query.Where(p => p.Name.ToLower().Contains(search));
        }

        var totalCount = await query.CountAsync();
        var items = await query
            .Skip((parameters.Page - 1) * parameters.PageSize)
            .Take(parameters.PageSize)
            .ToListAsync();

        return new PaginatedResult<Permission>
        {
            Items = items,
            TotalCount = totalCount,
            Page = parameters.Page,
            PageSize = parameters.PageSize
        };
    }
}
