using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using TicketingService.Api.Authorization;
using TicketingService.Core.DTOs.Comments;
using TicketingService.Core.Interfaces.Services;

namespace TicketingService.Api.Controllers;

/// <summary>
/// O5.3 — service-to-service only. Called by OperationsService when a service request advances
/// (quotation sent/approved, dispatched, completed, rejected) so the linked helpdesk ticket carries
/// an internal note and support has visibility. Protected by [ServiceKeyAuthorize].
///
/// <para>The key check was inline and used a plain <c>!=</c> comparison, which leaks the secret
/// through timing. The attribute compares in constant time and accepts both the legacy
/// <c>X-Service-Key</c> header and the platform's <c>X-Internal-Key</c>.</para>
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/internal/service-request-status")]
[ServiceKeyAuthorize]
public class InternalServiceRequestStatusController(
    ITicketService ticketService,
    ILogger<InternalServiceRequestStatusController> logger) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> StatusUpdate([FromBody] ServiceRequestStatusRequest request)
    {

        if (string.IsNullOrWhiteSpace(request.TicketId))
            return BadRequest(new { success = false, message = "ticketId is required" });

        var note = string.IsNullOrWhiteSpace(request.Note)
            ? $"[Service Request {request.ReferenceNumber}] status → {request.Status}"
            : $"[Service Request {request.ReferenceNumber}] {request.Note}";

        try
        {
            await ticketService.AddCommentAsync(
                request.TicketId,
                new CreateCommentDto { TicketId = request.TicketId, Content = note, IsInternal = true },
                "operations-service");
        }
        catch (Exception ex)
        {
            // The ticket may not exist (e.g. deleted) — log and acknowledge; the SR advance already succeeded.
            logger.LogWarning(ex, "Could not attach SR-status note to ticket {TicketId}", request.TicketId);
            return Ok(new { success = false, message = "Ticket note not attached" });
        }

        logger.LogInformation("SR {Ref} status {Status} noted on ticket {TicketId}",
            request.ReferenceNumber, request.Status, request.TicketId);
        return Ok(new { success = true });
    }
}

public class ServiceRequestStatusRequest
{
    public string TicketId        { get; set; } = string.Empty;
    public string ReferenceNumber { get; set; } = string.Empty;
    public string Status          { get; set; } = string.Empty;
    public string? Note           { get; set; }
}
