using Microsoft.AspNetCore.Mvc;
using TicketingService.Api.Authorization;
using TicketingService.Infrastructure.Services;

namespace TicketingService.Api.Controllers;

/// <summary>
/// Service-to-service provisioning endpoint, authenticated by the shared internal key (not JWT).
/// The user-service orchestrator calls this to create+migrate the ticketing tenant schema.
/// </summary>
[ApiController]
[Route("internal/tenants")]
[ServiceKeyAuthorize]
public class InternalProvisioningController(ITenantProvisioningService provisioning, ILogger<InternalProvisioningController> logger)
    : ControllerBase
{
    [HttpPost("provision")]
    public async Task<IActionResult> Provision([FromBody] ProvisionRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Schema))
            return BadRequest(new { message = "schema is required." });

        logger.LogInformation("Internal provision requested for tenant {TenantId} schema {Schema}", request.TenantId, request.Schema);
        var result = await provisioning.ProvisionAsync(request.Schema, ct);

        return result.Success
            ? Ok(new { result.Success, result.Schema })
            : StatusCode(StatusCodes.Status500InternalServerError, new { result.Success, result.Schema, result.Error });
    }

    public record ProvisionRequest(string TenantId, string Schema);
}
