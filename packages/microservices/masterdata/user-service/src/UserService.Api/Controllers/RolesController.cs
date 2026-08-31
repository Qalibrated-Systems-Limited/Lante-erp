using Asp.Versioning;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UserService.Core.DTOs.Common;
using UserService.Core.DTOs.Permissions;
using UserService.Core.DTOs.Roles;
using UserService.Core.Interfaces.Services;

namespace UserService.Api.Controllers;

[Authorize]
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/roles")]
public class RolesController(
    IRoleService roleService,
    IMapper mapper)
    : BaseController
{
    [HttpGet]
    [Authorize(Policy = "roles.manage")]
    public async Task<IActionResult> GetAll()
    {
        var roles = await roleService.GetAllAsync();
        return OkResult(roles);
    }

    [HttpGet("{id}")]
    [Authorize(Policy = "roles.manage")]
    public async Task<IActionResult> GetById(string id)
    {
        var role = await roleService.GetByIdAsync(id);
        if (role == null) return NotFoundResult($"Role with ID {id} not found.");
        return OkResult(role);
    }

    [HttpPost]
    [Authorize(Policy = "roles.manage")]
    public async Task<IActionResult> Create([FromBody] CreateRoleDto dto)
    {
        if (!ModelState.IsValid) return BadRequestResult("Invalid request data.");
        var role = await roleService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = role.Id }, new { Success = true, Data = role, StatusCode = 201 });
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "roles.manage")]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateRoleDto dto)
    {
        var role = await roleService.UpdateAsync(id, dto);
        if (role == null) return NotFoundResult($"Role with ID {id} not found.");
        return OkResult(role);
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "roles.manage")]
    public async Task<IActionResult> Delete(string id)
    {
        var result = await roleService.DeleteAsync(id);
        if (!result) return NotFoundResult($"Role with ID {id} not found.");
        return OkResult(new { message = "Role deleted successfully." });
    }

    [HttpGet("{roleId}/permissions")]
    [Authorize(Policy = "roles.manage")]
    public async Task<IActionResult> GetPermissions(string roleId)
    {
        var permissions = await roleService.GetPermissionsForRoleAsync(roleId);
        return OkResult(mapper.Map<IEnumerable<PermissionReadDto>>(permissions));
    }

    [HttpPost("{roleId}/permissions/{permissionId}")]
    [Authorize(Policy = "roles.manage")]
    public async Task<IActionResult> AddPermission(string roleId, string permissionId)
    {
        await roleService.AssignPermissionToRoleAsync(roleId, permissionId);
        return OkResult(new { message = "Permission assigned to role." });
    }

    [HttpDelete("{roleId}/permissions/{permissionId}")]
    [Authorize(Policy = "roles.manage")]
    public async Task<IActionResult> RemovePermission(string roleId, string permissionId)
    {
        var removed = await roleService.RemovePermissionFromRoleAsync(roleId, permissionId);
        if (!removed) return NotFoundResult("Permission not found on role.");
        return OkResult(new { message = "Permission removed from role." });
    }

    [HttpGet("deleted")]
    [Authorize(Policy = "roles.manage")]
    public async Task<IActionResult> GetDeleted([FromQuery] PaginationParameters parameters)
    {
        var result = await roleService.GetDeletedPagedAsync(parameters);
        return OkResult(result);
    }
}
