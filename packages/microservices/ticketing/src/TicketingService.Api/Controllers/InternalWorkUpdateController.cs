using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using TicketingService.Api.Authorization;
using TicketingService.Core.Enums;
using TicketingService.Core.Interfaces.Services;

namespace TicketingService.Api.Controllers;

/// <summary>
/// Service-to-service only. Called by OperationsService and FleetService when work is done.
/// Protected by [ServiceKeyAuthorize], not a user JWT.
///
/// <para>The key check was inline here and compared the secret with a plain <c>!=</c>, which
/// short-circuits at the first differing byte and so leaks it through timing. The attribute uses
/// a constant-time comparison, as the other twenty-five internal controllers already did. It also
/// accepts this endpoint's legacy <c>X-Service-Key</c> header alongside the platform's
/// <c>X-Internal-Key</c>, so the operations and fleet callers keep working unchanged.</para>
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/internal/work-update")]
[ServiceKeyAuthorize]
public class InternalWorkUpdateController(
    ITicketService ticketService,
    ILogger<InternalWorkUpdateController> logger) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> WorkCompleted([FromBody] WorkUpdateRequest request)
    {

        var (found, newStatus) = await ticketService.ApplyWorkUpdateAsync(
            request.TicketId, request.UpdateType, request.Source, request.Notes);

        if (!found)
        {
            logger.LogWarning("Work-update received for unknown ticket {TicketId}", request.TicketId);
            return NotFound(new { success = false, message = "Ticket not found" });
        }

        logger.LogInformation(
            "Ticket {TicketId} updated to {Status} by {Source} (ref: {ExternalRef})",
            request.TicketId, newStatus, request.Source, request.ExternalReferenceId);

        // O5.5 — the SR now lives in operations, which completes it itself when the linked assignment
        // completes (AssignmentService.CompleteAsync) or a certificate is issued. The old ticketing-side
        // SR completion (#11) was removed with the SR migration.

        return Ok(new { success = true, ticketId = request.TicketId, newStatus });
    }
}

public class WorkUpdateRequest
{
    public string TicketId { get; set; } = string.Empty;
    /// <summary>Which service sent this — e.g. "OperationsService", "FleetService"</summary>
    public string Source { get; set; } = string.Empty;
    /// <summary>The assignment ID or trip ID that was completed</summary>
    public string ExternalReferenceId { get; set; } = string.Empty;
    public WorkUpdateType UpdateType { get; set; }
    public string? Notes { get; set; }
}
