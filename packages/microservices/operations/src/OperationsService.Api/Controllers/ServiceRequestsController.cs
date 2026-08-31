using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OperationsService.Core.DTOs.ServiceRequests;
using OperationsService.Core.Interfaces.Services;

namespace OperationsService.Api.Controllers;

/// <summary>
/// O5 — TM endpoints for the Service &amp; Calibration Request queue: review submissions, build and
/// send quotations, record LPOs / certificates, and dispatch approved requests to field operations.
/// Migrated from ticketing; the route/response shapes are preserved so the gateway can repoint
/// <c>/service-requests</c> here (O5.4) without any frontend change.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/service-requests")]
[Authorize]
public class ServiceRequestsController(
    IServiceRequestService service) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    // ── GET / — list ──────────────────────────────────────────────────────────

    [HttpGet]
    [Authorize(Policy = "Permission:operations.read.own")]
    public async Task<IActionResult> GetAll([FromQuery] ServiceRequestFilterParams filter)
    {
        var result = await service.GetAllAsync(filter);
        return Ok(new
        {
            data     = result.Items,
            total    = result.Total,
            page     = filter.Page,
            pageSize = filter.PageSize,
            pages    = (int)Math.Ceiling(result.Total / (double)filter.PageSize),
        });
    }

    // ── POST / — staff-side create (client who won't use the portal) ──────────

    [HttpPost]
    [Authorize(Policy = "Permission:operations.write")]
    public async Task<IActionResult> Create([FromBody] IngestServiceRequestDto dto)
    {
        var result = await service.CreateAsync(dto, UserId);
        return Ok(new { data = new { id = result.Id, referenceNumber = result.ReferenceNumber, status = result.Status } });
    }

    // ── GET /{id} — detail ────────────────────────────────────────────────────

    [HttpGet("{id}")]
    [Authorize(Policy = "Permission:operations.read.own")]
    public async Task<IActionResult> GetById(string id)
    {
        var sr = await service.GetByIdAsync(id);
        if (sr == null) return NotFound(new { message = "Service request not found." });
        return Ok(new { data = sr });
    }

    // ── POST /{id}/review — approve / reject ──────────────────────────────────

    [HttpPost("{id}/review")]
    [Authorize(Policy = "Permission:operations.write")]
    public async Task<IActionResult> Review(string id, [FromBody] TmReviewDto dto)
    {
        var result = await service.ReviewAsync(id, dto, UserId);
        return Ok(new { data = new { status = result.Status, message = result.Message } });
    }

    // ── POST /{id}/quotation — create quotation draft ─────────────────────────

    [HttpPost("{id}/quotation")]
    [Authorize(Policy = "Permission:operations.write")]
    public async Task<IActionResult> CreateQuotation(string id, [FromBody] CreateQuotationDto dto)
    {
        var quotation = await service.CreateQuotationAsync(id, dto, UserId);
        return Ok(new { data = quotation });
    }

    // ── PUT /{id}/quotation — update quotation draft ──────────────────────────

    [HttpPut("{id}/quotation")]
    [Authorize(Policy = "Permission:operations.write")]
    public async Task<IActionResult> UpdateQuotation(string id, [FromBody] UpdateQuotationDto dto)
    {
        var quotation = await service.UpdateQuotationAsync(id, dto, UserId);
        return Ok(new { data = quotation });
    }

    // ── POST /{id}/quotation/send — email quotation to client ─────────────────

    [HttpPost("{id}/quotation/send")]
    [Authorize(Policy = "Permission:operations.write")]
    public async Task<IActionResult> SendQuotation(string id)
    {
        var result = await service.SendQuotationAsync(id);
        return Ok(new { data = new { message = result.Message, sentAt = result.SentAt } });
    }

    // ── POST /{id}/quotation/lpo — record client LPO / acceptance ─────────────

    [HttpPost("{id}/quotation/lpo")]
    [Authorize(Policy = "Permission:operations.write")]
    public async Task<IActionResult> RecordLpo(string id, [FromBody] RecordLpoDto dto)
    {
        var result = await service.RecordLpoAsync(id, dto);
        return Ok(new { data = new { message = result.Message, quotationStatus = result.QuotationStatus } });
    }

    // ── POST /{id}/quotation/revise — reopen a sent/rejected/expired quotation ─

    [HttpPost("{id}/quotation/revise")]
    [Authorize(Policy = "Permission:operations.write")]
    public async Task<IActionResult> ReviseQuotation(string id)
    {
        var result = await service.ReviseQuotationAsync(id);
        return Ok(new { data = new { message = result.Message, quotationStatus = result.QuotationStatus } });
    }

    // ── POST /{id}/quotation/reject — client declines a sent quotation ────────

    [HttpPost("{id}/quotation/reject")]
    [Authorize(Policy = "Permission:operations.write")]
    public async Task<IActionResult> RejectQuotation(string id, [FromBody] RejectQuotationDto dto)
    {
        var result = await service.RejectQuotationAsync(id, dto, UserId);
        return Ok(new { data = new { status = result.Status, message = result.Message } });
    }

    // ── POST /{id}/reject — quick reject without a full review dto ─────────────

    [HttpPost("{id}/reject")]
    [Authorize(Policy = "Permission:operations.write")]
    public async Task<IActionResult> Reject(string id, [FromBody] TmReviewDto dto)
    {
        dto.Approve = false;
        return await Review(id, dto);
    }

    // ── POST /{id}/cancel — cancel a request before field work begins ──────────

    [HttpPost("{id}/cancel")]
    [Authorize(Policy = "Permission:operations.write")]
    public async Task<IActionResult> Cancel(string id, [FromBody] CancelServiceRequestDto dto)
    {
        var result = await service.CancelAsync(id, dto, UserId);
        return Ok(new { data = new { status = result.Status, message = result.Message } });
    }

    // ── POST /{id}/certificate — record certificate number ────────────────────

    [HttpPost("{id}/certificate")]
    [Authorize(Policy = "Permission:operations.write")]
    public async Task<IActionResult> RecordCertificate(string id, [FromBody] RecordCertificateDto dto)
    {
        var result = await service.RecordCertificateAsync(id, dto);
        return Ok(new { data = new { certificateNumber = result.CertificateNumber, status = result.Status, message = result.Message } });
    }

    // ── POST /{id}/create-assignment — dispatch to field operations (in-service) ─

    [HttpPost("{id}/create-assignment")]
    [Authorize(Policy = "Permission:operations.write")]
    public async Task<IActionResult> CreateAssignment(string id, [FromBody] CreateSrAssignmentDto dto)
    {
        var result = await service.CreateAssignmentAsync(id, dto, UserId);
        return Ok(new { data = new { assignmentId = result.AssignmentId, message = result.Message } });
    }
}
