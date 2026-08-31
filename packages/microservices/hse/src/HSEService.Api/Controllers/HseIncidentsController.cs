using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HSEService.Core.DTOs.Common;
using HSEService.Core.DTOs.Incidents;
using HSEService.Core.Entities;
using HSEService.Core.Interfaces.Services;

namespace HSEService.Api.Controllers;

// HSE-001/007: incident capture — near-miss, first aid, medical treatment, lost-time injury,
// with optional environmental (NEMA) extension and an optional first corrective action, all in
// one create call. All persistence goes through IHseIncidentWorkflowService — never a repository.
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/hse-incidents")]
public class HseIncidentsController(IHseIncidentWorkflowService workflow, IHseCrudService<HseIncident> incidents, IHseCrudService<CorrectiveAction> correctiveActions) : ControllerBase
{
    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
    private string? CurrentUserName => User.FindFirstValue("name") ?? User.FindFirstValue(ClaimTypes.Name);
    private string? CurrentTenantSchema => User.FindFirstValue("schema");

    [HttpGet]
    [Authorize(Policy = "hse.read")]
    public async Task<ActionResult<ApiResponse<PaginatedResult<HseIncidentReadDto>>>> GetAll([FromQuery] PaginationParameters parameters)
    {
        var paged = await workflow.GetPagedWithDetailsAsync(parameters);
        var dtoItems = paged.Items.Select(ToReadDto).ToList();
        var result = new PaginatedResult<HseIncidentReadDto>
        {
            Items = dtoItems,
            TotalCount = paged.TotalCount,
            Page = paged.Page,
            PageSize = paged.PageSize
        };
        return Ok(ApiResponse<PaginatedResult<HseIncidentReadDto>>.Ok(result));
    }

    [HttpGet("{id}")]
    [Authorize(Policy = "hse.read")]
    public async Task<ActionResult<ApiResponse<HseIncidentReadDto>>> GetById(string id)
    {
        var incident = await workflow.GetWithDetailsAsync(id);
        if (incident == null) return NotFound(ApiResponse<HseIncidentReadDto>.Fail("Incident not found.", 404));
        return Ok(ApiResponse<HseIncidentReadDto>.Ok(ToReadDto(incident)));
    }

    [HttpPost]
    [Authorize(Policy = "hse.write")]
    public async Task<ActionResult<ApiResponse<HseIncidentReadDto>>> Create([FromBody] CreateHseIncidentDto dto)
    {
        var incident = await workflow.CreateAsync(dto, CurrentUserId, CurrentUserName, CurrentTenantSchema);
        return CreatedAtAction(nameof(GetById), new { id = incident.Id, version = "1" },
            ApiResponse<HseIncidentReadDto>.Ok(ToReadDto(incident), "Incident reported. CAPA required within 48 hours."));
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "hse.write")]
    public async Task<ActionResult<ApiResponse<HseIncidentReadDto>>> Update(string id, [FromBody] UpdateHseIncidentDto dto)
    {
        var incident = await incidents.GetByIdAsync(id);
        if (incident == null) return NotFound(ApiResponse<HseIncidentReadDto>.Fail("Incident not found.", 404));

        incident.SiteId = dto.SiteId;
        incident.SiteName = dto.SiteName;
        incident.Type = dto.Type;
        incident.Severity = dto.Severity;
        incident.OccurredAt = dto.OccurredAt;
        incident.Description = dto.Description;
        await incidents.UpdateAsync(incident);

        var withDetails = await workflow.GetWithDetailsAsync(id);
        return Ok(ApiResponse<HseIncidentReadDto>.Ok(ToReadDto(withDetails!), "Incident updated."));
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "hse.delete")]
    public async Task<ActionResult<ApiResponse>> Delete(string id)
    {
        var deleted = await incidents.DeleteAsync(id);
        if (!deleted) return NotFound(new ApiResponse { Success = false, Message = "Incident not found.", StatusCode = 404 });
        return Ok(new ApiResponse { Success = true, Message = "Incident deleted successfully.", StatusCode = 200 });
    }

    [HttpPost("{id}/corrective-actions")]
    [Authorize(Policy = "hse.write")]
    public async Task<ActionResult<ApiResponse<CorrectiveActionReadDto>>> AddCorrectiveAction(string id, [FromBody] CreateCorrectiveActionDto dto)
    {
        dto.IncidentId = id;
        var capa = await workflow.AddCorrectiveActionAsync(dto, CurrentTenantSchema);
        return Ok(ApiResponse<CorrectiveActionReadDto>.Ok(ToReadDto(capa), "Corrective action added."));
    }

    // Moves the incident through Open → Under Investigation → CAPA Pending → Closed. Any status
    // can be set directly (e.g. a near-miss with no CAPA can close immediately) — this isn't
    // enforced as a strict linear sequence.
    [HttpPatch("{id}/status")]
    [Authorize(Policy = "hse.approve")]
    public async Task<ActionResult<ApiResponse<HseIncidentReadDto>>> UpdateStatus(string id, [FromBody] UpdateHseIncidentStatusDto dto)
    {
        var incident = await incidents.GetByIdAsync(id);
        if (incident == null) return NotFound(ApiResponse<HseIncidentReadDto>.Fail("Incident not found.", 404));

        incident.Status = dto.Status;
        await incidents.UpdateAsync(incident);

        var withDetails = await workflow.GetWithDetailsAsync(id);
        return Ok(ApiResponse<HseIncidentReadDto>.Ok(ToReadDto(withDetails!), "Incident status updated."));
    }

    [HttpPatch("corrective-actions/{capaId}")]
    [Authorize(Policy = "hse.write")]
    public async Task<ActionResult<ApiResponse<CorrectiveActionReadDto>>> UpdateCorrectiveAction(string capaId, [FromBody] UpdateCorrectiveActionDto dto)
    {
        var capa = await correctiveActions.GetByIdAsync(capaId);
        if (capa == null) return NotFound(ApiResponse<CorrectiveActionReadDto>.Fail("Corrective action not found.", 404));

        capa.Description = dto.Description;
        capa.OwnerUserId = dto.OwnerUserId;
        capa.OwnerName = dto.OwnerName;
        capa.Status = dto.Status;
        capa.DueDate = dto.DueDate;
        capa.ClosedAt = dto.Status == Core.Enums.CorrectiveActionStatus.Completed ? (capa.ClosedAt ?? DateTime.UtcNow) : null;
        await correctiveActions.UpdateAsync(capa);

        return Ok(ApiResponse<CorrectiveActionReadDto>.Ok(ToReadDto(capa), "Corrective action updated."));
    }

    private static HseIncidentReadDto ToReadDto(HseIncident i) => new()
    {
        Id = i.Id,
        SiteId = i.SiteId,
        SiteName = i.SiteName,
        Type = i.Type,
        Severity = i.Severity,
        OccurredAt = i.OccurredAt,
        ReportedByUserId = i.ReportedByUserId,
        ReportedByName = i.ReportedByName,
        Description = i.Description,
        Status = i.Status,
        CreatedAt = i.CreatedAt,
        EnvIncident = i.EnvIncident == null ? null : new EnvIncidentDto
        {
            Id = i.EnvIncident.Id,
            IncidentId = i.EnvIncident.IncidentId,
            NemaRef = i.EnvIncident.NemaRef,
            NemaNotificationRequired = i.EnvIncident.NemaNotificationRequired,
            NotifiedAt = i.EnvIncident.NotifiedAt,
        },
        CorrectiveActions = i.CorrectiveActions.Select(ToReadDto).ToList(),
    };

    private static CorrectiveActionReadDto ToReadDto(CorrectiveAction c) => new()
    {
        Id = c.Id,
        IncidentId = c.IncidentId,
        Description = c.Description,
        OwnerUserId = c.OwnerUserId,
        OwnerName = c.OwnerName,
        Status = c.Status,
        DueDate = c.DueDate,
        ClosedAt = c.ClosedAt,
    };
}
