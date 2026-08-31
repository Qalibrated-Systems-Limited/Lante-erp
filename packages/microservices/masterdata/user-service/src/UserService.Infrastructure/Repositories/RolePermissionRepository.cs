using Microsoft.EntityFrameworkCore;
using UserService.Core.Entities;
using UserService.Core.Interfaces.Repositories;
using UserService.Infrastructure.Data;

namespace UserService.Infrastructure.Repositories;

public class RolePermissionRepository(LanteUserServiceDbContext context)
    : GenericRepository<RolePermission>(context), IRolePermissionRepository
{
    public async Task<RolePermission?> GetByRoleAndPermissionAsync(string roleId, string permissionId)
    {
        return await Context.RolePermissions
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(rp => rp.RoleId == roleId && rp.PermissionId == permissionId && !rp.IsDeleted);
    }

    public async Task<IEnumerable<RolePermission>> GetByRoleIdAsync(string roleId)
    {
        return await Context.RolePermissions
            .Where(rp => rp.RoleId == roleId && !rp.IsDeleted)
            .Include(rp => rp.Permission)
            .ToListAsync();
    }
}
