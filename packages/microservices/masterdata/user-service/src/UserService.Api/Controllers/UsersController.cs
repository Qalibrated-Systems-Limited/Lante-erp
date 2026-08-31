using Asp.Versioning;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UserService.Core.DTOs.Common;
using UserService.Core.DTOs.Roles;
using UserService.Core.DTOs.Users;
using UserService.Core.Interfaces.Services;

namespace UserService.Api.Controllers;

[Authorize]
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/users")]
public class UsersController(
    IUserService userService,
    IMapper mapper)
    : BaseController
{
    [HttpGet("{id}")]
    [Authorize(Policy = "users.read")]
    public async Task<IActionResult> GetById(string id)
    {
        var user = await userService.GetByIdAsync(id);
        if (user == null) return NotFoundResult($"User with ID {id} not found.");
        return OkResult(user);
    }

    [HttpGet]
    [Authorize(Policy = "users.read")]
    public async Task<IActionResult> GetPaged([FromQuery] PaginationParameters parameters)
    {
        var hasWriteAll = User.HasClaim("permission", "users.write");
        var deptIdsClaim = User.FindFirst("department_ids")?.Value;

        if (!hasWriteAll && !string.IsNullOrEmpty(deptIdsClaim))
        {
            var deptIds = deptIdsClaim.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
            var result = await userService.GetFilteredPagedAsync(new UserFilterParameters
            {
                Page = parameters.Page,
                PageSize = parameters.PageSize,
                Search = parameters.Search,
                DepartmentIds = deptIds
            });
            return OkResult(result);
        }

        return OkResult(await userService.GetPagedAsync(parameters));
    }

    [HttpGet("deleted")]
    [Authorize(Policy = "users.delete")]
    public async Task<IActionResult> GetDeletedPaged([FromQuery] PaginationParameters parameters)
    {
        var result = await userService.GetDeletedPagedAsync(parameters);
        return OkResult(result);
    }

    [HttpPost]
    [Authorize(Policy = "users.write")]
    public async Task<IActionResult> Create([FromBody] CreateUserDto dto)
    {
        if (!ModelState.IsValid) return BadRequestResult("Invalid request data.");
        // Inject tenant context from JWT/gateway headers — not set by frontend directly
        dto.TenantId ??= User.FindFirst("tenant_id")?.Value
                       ?? Request.Headers["X-Tenant-Id"].FirstOrDefault();
        var user = await userService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = user.Id }, new { Success = true, Data = user, StatusCode = 201 });
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "users.write")]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateUserDto dto)
    {
        var user = await userService.UpdateAsync(id, dto);
        if (user == null) return NotFoundResult($"User with ID {id} not found.");
        return OkResult(user);
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "users.delete")]
    public async Task<IActionResult> Delete(string id)
    {
        var result = await userService.DeleteAsync(id);
        if (!result) return NotFoundResult($"User with ID {id} not found.");
        return OkResult(new { message = "User deleted successfully." });
    }

    [HttpPatch("{id}/restore")]
    [Authorize(Policy = "users.write")]
    public async Task<IActionResult> Restore(string id)
    {
        var result = await userService.RestoreAsync(id);
        if (!result) return NotFoundResult($"Deleted user with ID {id} not found.");
        return OkResult(new { message = "User restored successfully." });
    }

    // IsActive purely tracks whether the user is currently logged in — it's set automatically on
    // login/logout (see AuthController) and was never actually checked to block login, so an admin
    // "deactivate" toggle here never did anything. Repurposed as a two-factor on/off control instead.
    [HttpPut("{id}/enable-2fa")]
    [Authorize(Policy = "users.write")]
    public async Task<IActionResult> EnableTwoFactor(string id)
    {
        await userService.SetTwoFactorEnabledAsync(id, true);
        return OkResult(new { message = "Two-factor authentication enabled." });
    }

    [HttpPut("{id}/disable-2fa")]
    [Authorize(Policy = "users.write")]
    public async Task<IActionResult> DisableTwoFactor(string id)
    {
        await userService.SetTwoFactorEnabledAsync(id, false);
        return OkResult(new { message = "Two-factor authentication disabled." });
    }

    [HttpPost("{userId}/reset-password")]
    [Authorize(Policy = "users.write")]
    public async Task<IActionResult> ResetPassword(string userId)
    {
        var result = await userService.ResetUserPasswordAsync(userId);
        if (!result) return NotFoundResult($"User with ID {userId} not found.");
        return OkResult(new { message = "Password reset initiated." });
    }

    [HttpPost("{userId}/resend-invite")]
    [Authorize(Policy = "users.write")]
    public async Task<IActionResult> ResendInvite(string userId)
    {
        await userService.ResendInviteAsync(userId);
        return OkResult(new { message = "Invite resent." });
    }

    [HttpGet("{userId}/permissions")]
    [Authorize(Policy = "users.read")]
    public async Task<IActionResult> GetUserPermissions(string userId)
    {
        var permissions = await userService.GetUserPermissionsAsync(userId);
        var permissionNames = permissions.Select(p => p.Name).ToList();
        return OkResult(new { permissions = permissionNames, count = permissionNames.Count });
    }

    [HttpGet("{userId}/roles")]
    [Authorize(Policy = "users.read")]
    public async Task<IActionResult> GetUserRoles(string userId)
    {
        var roles = await userService.GetUserRolesByUserIdAsync(userId);
        var roleDtos = mapper.Map<IEnumerable<RoleReadDto>>(roles).ToList();
        return OkResult(new { roles = roleDtos, count = roleDtos.Count });
    }

    [HttpPost("{userId}/roles/{roleId}")]
    [Authorize(Policy = "users.write")]
    public async Task<IActionResult> AssignRole(string userId, string roleId)
    {
        await userService.AssignRoleToUserAsync(userId, roleId);
        return OkResult(new { message = "Role assigned to user." });
    }

    [HttpDelete("{userId}/roles/{roleId}")]
    [Authorize(Policy = "users.write")]
    public async Task<IActionResult> RemoveRole(string userId, string roleId)
    {
        var removed = await userService.RemoveRoleFromUserAsync(userId, roleId);
        if (!removed) return NotFoundResult("Role not found on user.");
        return OkResult(new { message = "Role removed from user." });
    }

    [HttpGet("notification-targets")]
    [Authorize(Policy = "users.read")]
    public async Task<IActionResult> GetNotificationTargets([FromQuery] string? departmentId)
    {
        var targets = await userService.GetNotificationTargetsAsync(departmentId);
        return OkResult(targets);
    }
}
