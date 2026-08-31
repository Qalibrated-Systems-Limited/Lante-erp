using UserService.Core.Entities;

namespace UserService.Core.Interfaces.Repositories;

public interface IRolePermissionRepository : IGenericRepository<RolePermission>
{
    Task<RolePermission?> GetByRoleAndPermissionAsync(string roleId, string permissionId);
    Task<IEnumerable<RolePermission>> GetByRoleIdAsync(string roleId);
}
