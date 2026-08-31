using AutoMapper;
using Microsoft.Extensions.Logging;
using UserService.Core.DTOs.Common;
using UserService.Core.DTOs.Roles;
using UserService.Core.Entities;
using UserService.Core.Interfaces.Repositories;
using UserService.Core.Interfaces.Services;

namespace UserService.Core.Services;

public class RoleService(
    IRoleRepository roleRepository,
    IPermissionsRepository permissionsRepository,
    IRolePermissionRepository rolePermissionRepository,
    IMapper mapper)
    : IRoleService
{
    public async Task<IEnumerable<RoleReadDto>> GetAllAsync()
    {
        var roles = await roleRepository.GetAllAsync();
        return mapper.Map<IEnumerable<RoleReadDto>>(roles);
    }

    public async Task<RoleReadDto?> GetByIdAsync(string id)
    {
        var role = await roleRepository.GetByIdAsync(id);
        return role == null ? null : mapper.Map<RoleReadDto>(role);
    }

    public async Task<RoleReadDto> CreateAsync(CreateRoleDto dto)
    {
        var existing = await roleRepository.GetByNameAsync(dto.Name);
        if (existing != null)
            throw new InvalidOperationException($"A role named '{dto.Name}' already exists.");

        var role = new Role
        {
            Name = dto.Name,
            Description = dto.Description,
            IsActive = dto.IsActive,
            IsSystem = false, // admin-created roles are always custom (editable)
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await roleRepository.CreateAsync(role);
        return mapper.Map<RoleReadDto>(role);
    }

    public async Task<RoleReadDto?> UpdateAsync(string id, UpdateRoleDto dto)
    {
        var role = await roleRepository.GetByIdAsync(id);
        if (role == null) throw new KeyNotFoundException($"Role {id} not found.");
        // This guard is not new — but until the seeder actually set IsSystem it could never fire, so the
        // platform operator role was renameable and deletable by any holder of roles.manage. Authorization
        // compares role NAMES, and two of those comparisons fail OPEN, so a rename was a silent security
        // change rather than a cosmetic one.
        // Covers renaming, deactivating and description edits alike — every field on this DTO. Deactivating
        // matters as much as renaming, because an inactive role stops matching the name comparisons just as
        // thoroughly. No separate check for it: a second guard behind this one could never execute, and an
        // unreachable guard reading as protection is the exact defect this change exists to remove.
        if (role.IsSystem) throw new InvalidOperationException("System roles cannot be modified.");

        if (dto.Name != null) role.Name = dto.Name;
        if (dto.Description != null) role.Description = dto.Description;
        if (dto.IsActive.HasValue) role.IsActive = dto.IsActive.Value;
        role.UpdatedAt = DateTime.UtcNow;

        await roleRepository.UpdateAsync(role);
        return mapper.Map<RoleReadDto>(role);
    }

    public async Task<bool> DeleteAsync(string id)
    {
        var role = await roleRepository.GetByIdAsync(id);
        if (role == null) throw new KeyNotFoundException($"Role {id} not found.");
        if (role.IsSystem) throw new InvalidOperationException("System roles cannot be deleted.");
        return await roleRepository.DeleteAsync(id);
    }

    public async Task<PaginatedResult<RoleReadDto>> GetDeletedPagedAsync(PaginationParameters parameters)
    {
        var result = await roleRepository.GetDeletedPagedAsync(parameters);
        return new PaginatedResult<RoleReadDto>
        {
            Items = mapper.Map<IEnumerable<RoleReadDto>>(result.Items),
            TotalCount = result.TotalCount,
            Page = result.Page,
            PageSize = result.PageSize
        };
    }

    public async Task AssignPermissionToRoleAsync(string roleId, string permissionId)
    {
        var role = await roleRepository.GetByIdAsync(roleId)
            ?? throw new KeyNotFoundException($"Role {roleId} not found.");
        if (role.IsSystem) throw new InvalidOperationException("Permissions of system roles cannot be changed.");
        var permission = await permissionsRepository.GetByIdAsync(permissionId)
            ?? throw new KeyNotFoundException($"Permission {permissionId} not found.");

        var existing = await rolePermissionRepository.GetByRoleAndPermissionAsync(roleId, permissionId);
        if (existing != null)
            throw new InvalidOperationException("This permission is already assigned to the role.");

        await rolePermissionRepository.CreateAsync(new RolePermission
        {
            RoleId = roleId,
            PermissionId = permissionId,
            AssignedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        });
    }

    public async Task<bool> RemovePermissionFromRoleAsync(string roleId, string permissionId)
    {
        var role = await roleRepository.GetByIdAsync(roleId)
            ?? throw new KeyNotFoundException($"Role {roleId} not found.");
        if (role.IsSystem) throw new InvalidOperationException("Permissions of system roles cannot be changed.");
        var existing = await rolePermissionRepository.GetByRoleAndPermissionAsync(roleId, permissionId);
        if (existing == null) return false;
        return await rolePermissionRepository.DeleteAsync(existing.Id);
    }

    public async Task<IEnumerable<Permission>> GetPermissionsForRoleAsync(string roleId)
    {
        return await roleRepository.GetPermissionsForRoleAsync(roleId);
    }
}
