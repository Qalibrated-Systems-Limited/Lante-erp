using Microsoft.AspNetCore.Mvc;
using UserService.Api.Authorization;
using UserService.Core.DTOs.Audit;
using UserService.Core.Interfaces.Services;

namespace UserService.Api.Controllers;

/// <summary>
/// Ingestion endpoint the gateway calls for every state-changing request it proxies (fire-and-
/// forget). Tenant context comes from the gateway's X-Tenant-Schema header, honored by
/// UserServiceTenantConnectionInterceptor as the fallback for service-key calls with no JWT.
/// </summary>
[ApiController]
[Route("internal/audit-log")]
[ServiceKeyAuthorize]
public class InternalAuditLogController(IAuditLogService auditLogService) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateAuditLogDto dto)
    {
        await auditLogService.CreateAsync(dto);
        return Ok();
    }
}
