using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UserService.Core.Interfaces.Services;

namespace UserService.Api.Controllers;

[Authorize(Policy = "system.admin")]
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/audit-log")]
public class AuditLogController(IAuditLogService auditLogService) : BaseController
{
    [HttpGet]
    public async Task<IActionResult> GetRecent([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        pageSize = Math.Clamp(pageSize, 1, 200);
        page = Math.Max(page, 1);
        var result = await auditLogService.GetRecentAsync(page, pageSize);
        return OkResult(result);
    }
}
