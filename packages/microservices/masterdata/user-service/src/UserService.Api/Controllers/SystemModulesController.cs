using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UserService.Core.Interfaces.Services;

namespace UserService.Api.Controllers;

// Per-tenant module on/off registry — read by any authenticated user (the sidebar needs it to
// know what to render); writes are settings.manage only.
[Authorize]
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/system-modules")]
public class SystemModulesController(ISystemModuleService moduleService) : BaseController
{
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var modules = await moduleService.GetAllAsync();
        return OkResult(modules);
    }

    [HttpPut("{moduleKey}/toggle")]
    [Authorize(Policy = "settings.manage")]
    public async Task<IActionResult> Toggle(string moduleKey, [FromBody] ToggleModuleRequest request)
    {
        try
        {
            var updated = await moduleService.ToggleAsync(moduleKey, request.Enabled);
            return OkResult(updated);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFoundResult(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequestResult(ex.Message);
        }
    }

    public record ToggleModuleRequest(bool Enabled);
}
