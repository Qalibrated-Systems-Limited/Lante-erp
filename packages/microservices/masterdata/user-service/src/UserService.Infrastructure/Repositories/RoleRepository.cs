using Microsoft.EntityFrameworkCore;
using UserService.Core.DTOs.Common;
using UserService.Core.Entities;
using UserService.Core.Interfaces.Repositories;
using UserService.Infrastructure.Data;

namespace UserService.Infrastructure.Repositories;

public class RoleRepository(LanteUserServiceDbContext context)
    : GenericRepository<Role>(context), IRoleRepository
{
    public override async Task<IEnumerable<Role>> GetAllAsync()
    {
        return await Context.Roles
            .Include(r => r.UserRoles.Where(ur => !ur.IsDeleted))
            .Include(r => r.RolePermissions.Where(rp => !rp.IsDeleted))
                .ThenInclude(rp => rp.Permission)
            .Where(r => !r.IsDeleted)
            .ToListAsync();
    }

    public override async Task<Role?> GetByIdAsync(string id, bool includeDeleted = false)
    {
        var query = Context.Roles
            .Include(r => r.UserRoles.Where(ur => !ur.IsDeleted))
            .Include(r => r.RolePermissions.Where(rp => !rp.IsDeleted))
                .ThenInclude(rp => rp.Permission)
            .AsQueryable();

        if (includeDeleted)
            query = query.IgnoreQueryFilters();

        return await query.FirstOrDefaultAsync(r => r.Id == id && (includeDeleted || !r.IsDeleted));
    }

    public async Task<Role?> GetByNameAsync(string name)
    {
        return await Context.Roles.FirstOrDefaultAsync(r => r.Name == name && !r.IsDeleted);
    }

    public async Task<IEnumerable<Permission>> GetPermissionsForRoleAsync(string roleId)
    {
        return await Context.RolePermissions
            .Where(rp => rp.RoleId == roleId && !rp.IsDeleted)
            .Include(rp => rp.Permission)
            .Select(rp => rp.Permission)
            .Where(p => !p.IsDeleted)
            .ToListAsync();
    }

    public async Task<PaginatedResult<Role>> GetDeletedPagedAsync(PaginationParameters parameters)
    {
        var query = Context.Roles.IgnoreQueryFilters().Where(r => r.IsDeleted);
        var totalCount = await query.CountAsync();
        var items = await query
            .Skip((parameters.Page - 1) * parameters.PageSize)
            .Take(parameters.PageSize)
            .ToListAsync();

        return new PaginatedResult<Role>
        {
            Items = items,
            TotalCount = totalCount,
            Page = parameters.Page,
            PageSize = parameters.PageSize
        };
    }
}
