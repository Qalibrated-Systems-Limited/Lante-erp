using Microsoft.AspNetCore.Mvc;
using TicketingService.Api.Authorization;
using TicketingService.Core.Interfaces.Services;

namespace TicketingService.Api.Controllers;

/// <summary>
/// Service-to-service endpoint: the central place any service pushes a tenant-wide alert into
/// (an expiring license, a driver license about to lapse, ...) rather than each service
/// maintaining its own separate alert table. Scoped by the caller's X-Tenant-Schema header, same
/// pattern as InternalNotificationsController.
/// </summary>
[ApiController]
[Route("internal/alerts")]
[ServiceKeyAuthorize]
public class InternalAlertsController(IAlertService alertService, ILogger<InternalAlertsController> logger) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateAlertRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Source) || string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Message))
            return BadRequest(new { message = "source, title, and message are required." });

        var tenantSchema = Request.Headers["X-Tenant-Schema"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(tenantSchema))
            return BadRequest(new { message = "X-Tenant-Schema header is required." });

        await alertService.CreateAsync(
            tenantSchema, request.Source, request.Severity ?? "Warning", request.Title, request.Message,
            request.TicketId, request.TicketTitle, request.AssignedToUserId, request.RequiredPermission);

        logger.LogInformation("Internal alert created: {Source} — {Title}", request.Source, request.Title);
        return Ok(new { success = true });
    }

    public record CreateAlertRequest(string Source, string? Severity, string Title, string Message, string? TicketId, string? TicketTitle, string? AssignedToUserId, string? RequiredPermission = null);
}
