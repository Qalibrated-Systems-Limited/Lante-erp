using UserService.Core.DTOs.Common;
using UserService.Core.DTOs.Roles;
using UserService.Core.Entities;

namespace UserService.Core.Interfaces.Services;

public interface IRoleService
{
    Task<IEnumerable<RoleReadDto>> GetAllAsync();
    Task<RoleReadDto?> GetByIdAsync(string id);
    Task<RoleReadDto> CreateAsync(CreateRoleDto dto);
    Task<RoleReadDto?> UpdateAsync(string id, UpdateRoleDto dto);
    Task<bool> DeleteAsync(string id);
    Task<PaginatedResult<RoleReadDto>> GetDeletedPagedAsync(PaginationParameters parameters);
    Task AssignPermissionToRoleAsync(string roleId, string permissionId);
    Task<bool> RemovePermissionFromRoleAsync(string roleId, string permissionId);
    Task<IEnumerable<Permission>> GetPermissionsForRoleAsync(string roleId);
}
