using Microsoft.AspNetCore.Mvc;
using UserService.Api.Authorization;
using UserService.Core.Interfaces.Repositories;
using UserService.Core.Interfaces.Services;

namespace UserService.Api.Controllers;

/// <summary>
/// Service-to-service user lookup — other services (e.g. ticketing-service's NotificationService,
/// composing an email/SMS for a cross-service event with no end-user JWT to forward) need a user's
/// contact info without an Authorize-gated JWT call. Scoped by the caller's X-Tenant-Schema header,
/// same pattern as ticketing-service's InternalAlertsController/InternalNotificationsController.
/// </summary>
[ApiController]
[Route("internal/users")]
[ServiceKeyAuthorize]
public class InternalUsersController(IUserService userService, IUserRepository userRepository) : ControllerBase
{
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        var user = await userService.GetByIdAsync(id);
        if (user == null) return NotFound(new { message = "User not found." });

        return Ok(new
        {
            success = true,
            data = new
            {
                id = user.Id,
                firstName = user.FirstName,
                lastName = user.LastName,
                email = user.Email,
                mobileNumber = user.MobileNumber,
            }
        });
    }

    // Fans a notification out to everyone holding a given permission (e.g. fleet.write) instead
    // of a single known user — used when an event has no one specific "owner" to notify.
    [HttpGet("by-permission/{permission}")]
    public async Task<IActionResult> GetByPermission(string permission)
    {
        var users = await userRepository.GetUsersByPermissionAsync(permission);
        return Ok(new
        {
            success = true,
            data = users.Select(u => new
            {
                id = u.Id,
                firstName = u.FirstName,
                lastName = u.LastName,
                email = u.Email,
                mobileNumber = u.MobileNumber,
            })
        });
    }

    // Narrower than by-permission — matches a specific role by display name (e.g. "Fleet
    // Manager"), not everyone who happens to hold a permission that role's job also requires.
    [HttpGet("by-role/{roleName}")]
    public async Task<IActionResult> GetByRoleName(string roleName)
    {
        var users = await userRepository.GetUsersByRoleNameAsync(roleName);
        return Ok(new
        {
            success = true,
            data = users.Select(u => new
            {
                id = u.Id,
                firstName = u.FirstName,
                lastName = u.LastName,
                email = u.Email,
                mobileNumber = u.MobileNumber,
            })
        });
    }
}
