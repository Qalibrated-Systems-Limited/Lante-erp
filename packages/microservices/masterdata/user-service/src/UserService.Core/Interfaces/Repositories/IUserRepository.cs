using UserService.Core.DTOs.Common;
using UserService.Core.Entities;

namespace UserService.Core.Interfaces.Repositories;

public interface IUserRepository : IGenericRepository<User>
{
    Task<User?> GetByEmailAsync(string email);
    Task<IEnumerable<Permission>> GetUserPermissionsAsync(string userId);
    Task<IEnumerable<UserRole>> GetUserRolesAsync(string userId);
    Task<IEnumerable<User>> GetUsersByRoleAsync(string roleId);
    Task<IEnumerable<User>> GetUsersByDepartmentAsync(string departmentId);
    Task<PaginatedResult<User>> GetDeletedPagedAsync(PaginationParameters parameters);
    Task<PaginatedResult<User>> GetFilteredPagedAsync(UserFilterParameters parameters);
    Task<bool> UpdateActiveStatusAsync(string userId, bool isActive);
    Task<bool> SetTwoFactorEnabledAsync(string userId, bool enabled);
    Task<bool> AddUserRoleAsync(string userId, string roleId);
    Task<bool> RemoveUserRoleAsync(string userId, string roleId);
    Task<IEnumerable<User>> GetDepartmentManagersAsync(string departmentId);
    Task<IEnumerable<User>> GetUsersByRoleIdAsync(string roleId);
    Task<IEnumerable<User>> GetUsersByPermissionAsync(string permissionCode);
    Task<IEnumerable<User>> GetUsersByRoleNameAsync(string roleName);
}
