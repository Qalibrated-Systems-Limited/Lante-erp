using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UserService.Core.DTOs.Common;
using UserService.Core.DTOs.Permissions;
using UserService.Core.Interfaces.Services;

namespace UserService.Api.Controllers;

[Authorize]
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/permissions")]
public class PermissionsController(
    IPermissionsService permissionsService)
    : BaseController
{
    [HttpGet]
    [Authorize(Policy = "permissions.manage")]
    public async Task<IActionResult> GetPaged([FromQuery] PaginationParameters parameters)
    {
        var result = await permissionsService.GetPagedAsync(parameters);
        return OkResult(result);
    }

    [HttpGet("{id}")]
    [Authorize(Policy = "permissions.manage")]
    public async Task<IActionResult> GetById(string id)
    {
        var permission = await permissionsService.GetByIdAsync(id);
        if (permission == null) return NotFoundResult($"Permission with ID {id} not found.");
        return OkResult(permission);
    }

    [HttpPost]
    [Authorize(Policy = "permissions.manage")]
    public async Task<IActionResult> Create([FromBody] CreatePermissionDto dto)
    {
        if (!ModelState.IsValid) return BadRequestResult("Invalid request data.");
        var permission = await permissionsService.CreateAsync(dto);
        return CreatedAtAction(nameof(GetById), new { id = permission.Id }, new { Success = true, Data = permission, StatusCode = 201 });
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "permissions.manage")]
    public async Task<IActionResult> Delete(string id)
    {
        var result = await permissionsService.DeleteAsync(id);
        if (!result) return NotFoundResult($"Permission with ID {id} not found.");
        return OkResult(new { message = "Permission deleted." });
    }
}
