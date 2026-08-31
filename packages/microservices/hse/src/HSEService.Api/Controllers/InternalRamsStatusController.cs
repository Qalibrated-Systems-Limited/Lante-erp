using Microsoft.AspNetCore.Mvc;
using HSEService.Api.Authorization;
using HSEService.Core.Entities;
using HSEService.Core.Enums;
using HSEService.Core.Interfaces.Services;

namespace HSEService.Api.Controllers;

/// <summary>
/// SUB-005 hard gate: SubcontractsService calls this before activating mobilization, to confirm
/// the subcontractor has an Approved RAMS record (optionally scoped to a specific site). Service-key
/// authenticated (not JWT) — the caller has no ASP.NET user identity, so the tenant schema is
/// resolved from the X-Tenant-Schema header via TenantDbConnectionInterceptor.
/// </summary>
[ApiController]
[Route("internal/rams-status")]
[ServiceKeyAuthorize]
public class InternalRamsStatusController(IHseCrudService<Rams> rams) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetStatus([FromQuery] string subcontractorId, [FromQuery] string? siteId)
    {
        if (string.IsNullOrWhiteSpace(subcontractorId))
            return BadRequest(new { message = "subcontractorId is required." });

        var matches = await rams.FindAsync(r =>
            r.SubcontractorId == subcontractorId &&
            r.Status == RamsStatus.Approved &&
            (siteId == null || r.SiteId == siteId));

        return Ok(new { approved = matches.Count > 0 });
    }
}
