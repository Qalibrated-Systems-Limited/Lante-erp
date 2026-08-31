using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ComplianceService.Core.DTOs.Breaches;
using ComplianceService.Core.DTOs.CaseActions;
using ComplianceService.Core.DTOs.Common;
using ComplianceService.Core.Entities;
using ComplianceService.Core.Enums;
using ComplianceService.Core.Interfaces.Repositories;
using ComplianceService.Core.Interfaces.Services;

namespace ComplianceService.Api.Controllers;

// COMP-005: incident record; 72-hour ODPC notification timer; remediation tracking.
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/compliance-data-breaches")]
public class DataBreachesController(
    IComplianceCrudService<DataBreach> breaches,
    IComplianceCrudService<CaseAction> caseActions,
    IDataBreachRepository breachesRepository) : ControllerBase
{
    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
    private string? CurrentUserName => User.FindFirstValue("name") ?? User.FindFirstValue(ClaimTypes.Name);

    [HttpGet]
    [Authorize(Policy = "compliance.read")]
    public async Task<ActionResult<ApiResponse<PaginatedResult<DataBreachReadDto>>>> GetAll([FromQuery] PaginationParameters parameters)
    {
        var paged = await breachesRepository.GetPagedAsync(parameters);
        var result = new PaginatedResult<DataBreachReadDto>
        {
            Items = paged.Items.Select(ToReadDto).ToList(),
            TotalCount = paged.TotalCount,
            Page = paged.Page,
            PageSize = paged.PageSize
        };
        return Ok(ApiResponse<PaginatedResult<DataBreachReadDto>>.Ok(result));
    }

    [HttpGet("{id}")]
    [Authorize(Policy = "compliance.read")]
    public async Task<ActionResult<ApiResponse<DataBreachReadDto>>> GetById(string id)
    {
        var b = await breaches.GetByIdAsync(id);
        if (b == null) return NotFound(ApiResponse<DataBreachReadDto>.Fail("Breach record not found.", 404));
        return Ok(ApiResponse<DataBreachReadDto>.Ok(ToReadDto(b)));
    }

    [HttpPost]
    [Authorize(Policy = "compliance.write")]
    public async Task<ActionResult<ApiResponse<DataBreachReadDto>>> Create([FromBody] CreateDataBreachDto dto)
    {
        var b = new DataBreach
        {
            OccurredAt = dto.OccurredAt,
            DiscoveredAt = dto.DiscoveredAt,
            Description = dto.Description,
            OdpcNotificationDueAt = dto.DiscoveredAt.AddHours(72),
        };
        await breaches.CreateAsync(b);
        return CreatedAtAction(nameof(GetById), new { id = b.Id, version = "1" },
            ApiResponse<DataBreachReadDto>.Ok(ToReadDto(b), "Breach logged — ODPC notification due within 72 hours of discovery."));
    }

    [HttpPatch("{id}")]
    [Authorize(Policy = "compliance.approve")]
    public async Task<ActionResult<ApiResponse<DataBreachReadDto>>> Update(string id, [FromBody] UpdateDataBreachDto dto)
    {
        var b = await breaches.GetByIdAsync(id);
        if (b == null) return NotFound(ApiResponse<DataBreachReadDto>.Fail("Breach record not found.", 404));

        b.Status = dto.Status;
        b.OdpcNotifiedAt = dto.OdpcNotifiedAt;
        b.RemediationNotes = dto.RemediationNotes;
        await breaches.UpdateAsync(b);
        return Ok(ApiResponse<DataBreachReadDto>.Ok(ToReadDto(b), "Breach record updated."));
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "compliance.write")]
    public async Task<ActionResult<ApiResponse<DataBreachReadDto>>> UpdateDetails(string id, [FromBody] UpdateDataBreachDetailsDto dto)
    {
        var b = await breaches.GetByIdAsync(id);
        if (b == null) return NotFound(ApiResponse<DataBreachReadDto>.Fail("Breach record not found.", 404));

        b.OccurredAt = dto.OccurredAt;
        b.DiscoveredAt = dto.DiscoveredAt;
        b.Description = dto.Description;
        b.OdpcNotificationDueAt = dto.DiscoveredAt.AddHours(72);
        await breaches.UpdateAsync(b);
        return Ok(ApiResponse<DataBreachReadDto>.Ok(ToReadDto(b), "Breach record updated."));
    }

    [HttpGet("{id}/actions")]
    [Authorize(Policy = "compliance.read")]
    public async Task<ActionResult<ApiResponse<IEnumerable<CaseActionReadDto>>>> GetActions(string id)
    {
        var actions = await caseActions.FindAsync(a => a.ParentType == CaseActionParentType.DataBreach && a.ParentId == id);
        return Ok(ApiResponse<IEnumerable<CaseActionReadDto>>.Ok(actions.OrderBy(a => a.LoggedAt).Select(ToActionDto)));
    }

    [HttpPost("{id}/actions")]
    [Authorize(Policy = "compliance.write")]
    public async Task<ActionResult<ApiResponse<CaseActionReadDto>>> AddAction(string id, [FromBody] CreateCaseActionDto dto)
    {
        var action = new CaseAction
        {
            ParentType = CaseActionParentType.DataBreach,
            ParentId = id,
            Note = dto.Note,
            LoggedByUserId = CurrentUserId,
            LoggedByName = CurrentUserName,
        };
        await caseActions.CreateAsync(action);
        return Ok(ApiResponse<CaseActionReadDto>.Ok(ToActionDto(action), "Remediation step logged."));
    }

    private static DataBreachReadDto ToReadDto(DataBreach b) => new()
    {
        Id = b.Id,
        OccurredAt = b.OccurredAt,
        DiscoveredAt = b.DiscoveredAt,
        Description = b.Description,
        OdpcNotificationDueAt = b.OdpcNotificationDueAt,
        OdpcNotifiedAt = b.OdpcNotifiedAt,
        Status = b.Status,
        RemediationNotes = b.RemediationNotes,
    };

    private static CaseActionReadDto ToActionDto(CaseAction a) => new()
    {
        Id = a.Id,
        ParentType = a.ParentType,
        ParentId = a.ParentId,
        Note = a.Note,
        LoggedByName = a.LoggedByName,
        LoggedAt = a.LoggedAt,
    };
}
