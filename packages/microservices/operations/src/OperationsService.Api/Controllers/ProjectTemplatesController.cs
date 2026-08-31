using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OperationsService.Core.DTOs.Common;
using OperationsService.Core.DTOs.Templates;
using OperationsService.Core.Interfaces.Services;

namespace OperationsService.Api.Controllers;

/// <summary>
/// PR4b — project templates and the recurring schedules that raise projects from them.
///
/// Authoring a template shapes every job raised from it, so it sits behind the approval permission
/// rather than plain write; using one is ordinary project creation.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Authorize]
public class ProjectTemplatesController(
    IProjectTemplateService templates,
    IRecurringProjectService recurring) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
    private static ApiResponse<T> Ok<T>(T data) => new() { Success = true, Data = data, StatusCode = 200 };

    // ── Templates ────────────────────────────────────────────────────────────

    [HttpGet("api/v{version:apiVersion}/project-templates")]
    [Authorize(Policy = "Permission:projects.read.own")]
    public async Task<ActionResult<ApiResponse<List<ProjectTemplateDto>>>> List([FromQuery] bool includeInactive = false)
        => Ok(await templates.GetAllAsync(includeInactive));

    [HttpGet("api/v{version:apiVersion}/project-templates/{templateId:guid}")]
    [Authorize(Policy = "Permission:projects.read.own")]
    public async Task<ActionResult<ApiResponse<ProjectTemplateDetailDto>>> Get(Guid templateId)
    {
        var t = await templates.GetByIdAsync(templateId.ToString());
        return t is null ? NotFound() : Ok(t);
    }

    [HttpPost("api/v{version:apiVersion}/project-templates")]
    [Authorize(Policy = "Permission:projects.approve")]
    public async Task<ActionResult<ApiResponse<ProjectTemplateDetailDto>>> Create([FromBody] SaveProjectTemplateDto dto)
        => Ok(await templates.SaveAsync(null, dto, UserId));

    [HttpPut("api/v{version:apiVersion}/project-templates/{templateId:guid}")]
    [Authorize(Policy = "Permission:projects.approve")]
    public async Task<ActionResult<ApiResponse<ProjectTemplateDetailDto>>> Update(Guid templateId, [FromBody] SaveProjectTemplateDto dto)
        => Ok(await templates.SaveAsync(templateId.ToString(), dto, UserId));

    [HttpDelete("api/v{version:apiVersion}/project-templates/{templateId:guid}")]
    [Authorize(Policy = "Permission:projects.approve")]
    public async Task<IActionResult> Deactivate(Guid templateId)
    {
        await templates.DeleteAsync(templateId.ToString(), UserId);
        return NoContent();
    }

    /// <summary>Raises a new Draft project from the template.</summary>
    [HttpPost("api/v{version:apiVersion}/project-templates/{templateId:guid}/instantiate")]
    [Authorize(Policy = "Permission:projects.write")]
    public async Task<ActionResult<ApiResponse<string>>> Instantiate(Guid templateId, [FromBody] InstantiateTemplateDto dto)
        => Ok(await templates.InstantiateAsync(templateId.ToString(), dto, UserId));

    // ── Recurring schedules ──────────────────────────────────────────────────

    [HttpGet("api/v{version:apiVersion}/recurring-projects")]
    [Authorize(Policy = "Permission:projects.read.own")]
    public async Task<ActionResult<ApiResponse<List<RecurringScheduleDto>>>> ListSchedules([FromQuery] bool includeInactive = false)
        => Ok(await recurring.GetAllAsync(includeInactive));

    [HttpPost("api/v{version:apiVersion}/recurring-projects")]
    [Authorize(Policy = "Permission:projects.approve")]
    public async Task<ActionResult<ApiResponse<RecurringScheduleDto>>> CreateSchedule([FromBody] SaveRecurringScheduleDto dto)
        => Ok(await recurring.SaveAsync(null, dto, UserId));

    [HttpPut("api/v{version:apiVersion}/recurring-projects/{scheduleId:guid}")]
    [Authorize(Policy = "Permission:projects.approve")]
    public async Task<ActionResult<ApiResponse<RecurringScheduleDto>>> UpdateSchedule(Guid scheduleId, [FromBody] SaveRecurringScheduleDto dto)
        => Ok(await recurring.SaveAsync(scheduleId.ToString(), dto, UserId));

    [HttpPost("api/v{version:apiVersion}/recurring-projects/{scheduleId:guid}/active")]
    [Authorize(Policy = "Permission:projects.approve")]
    public async Task<ActionResult<ApiResponse<RecurringScheduleDto>>> SetActive(Guid scheduleId, [FromQuery] bool active = true)
        => Ok(await recurring.SetActiveAsync(scheduleId.ToString(), active, UserId));

    /// <summary>Raises the next occurrence immediately, without waiting for its lead-time window.</summary>
    [HttpPost("api/v{version:apiVersion}/recurring-projects/{scheduleId:guid}/run-now")]
    [Authorize(Policy = "Permission:projects.write")]
    public async Task<ActionResult<ApiResponse<string>>> RunNow(Guid scheduleId)
        => Ok(await recurring.RunNowAsync(scheduleId.ToString(), UserId));
}
