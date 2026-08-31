using Microsoft.AspNetCore.Mvc;
using OperationsService.Api.Authorization;
using OperationsService.Core.DTOs.ServiceRequests;
using OperationsService.Core.Interfaces.Services;

namespace OperationsService.Api.Controllers;

/// <summary>
/// O5.3 — service-to-service SR intake (internal-key auth). The ticketing portal calls this after it
/// verifies the submitter's email OTP and creates the tracking ticket, handing the verified request
/// to operations, which becomes the SR system of record. Guarded by <c>X-Internal-Key</c>.
/// </summary>
[ApiController]
[Route("internal/service-requests")]
[ServiceKeyAuthorize]
public class InternalServiceRequestsController(
    IServiceRequestService service,
    ILogger<InternalServiceRequestsController> logger) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Ingest([FromBody] IngestServiceRequestDto dto)
    {
        var result = await service.IngestAsync(dto);
        logger.LogInformation("Internal SR intake: {Ref} (ticket {TicketId})", result.ReferenceNumber, dto.TicketId);
        return Ok(new { data = result });
    }

    // Public portal tracking, proxied by ticketing (which owns the anonymous public surface).
    [HttpGet("track/{reference}")]
    public async Task<IActionResult> Track(string reference)
    {
        var summary = await service.TrackAsync(reference);
        if (summary == null) return NotFound(new { message = "No service request found with that reference." });
        return Ok(new { data = summary });
    }

    [HttpPost("{reference}/signature")]
    public async Task<IActionResult> Signature(string reference, [FromBody] SignatureBody body)
    {
        var result = await service.SubmitSignatureAsync(reference, body.SignatureData);
        return Ok(new { data = new { message = result.Message } });
    }

    public class SignatureBody
    {
        public string SignatureData { get; set; } = string.Empty;
    }
}
