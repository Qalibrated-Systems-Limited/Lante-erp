using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CrmService.Core.DTOs.Activity;
using CrmService.Core.Interfaces.Services;

namespace CrmService.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}")]
[Authorize]
public class ActivityController(IActivityService service) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    // ── Interactions (nested under a customer) ──
    [HttpGet("customers/{customerId}/interactions")]
    [Authorize(Policy = "Permission:crm.read.own")]
    public async Task<IActionResult> Interactions(string customerId) => Ok(new { data = await service.GetInteractionsAsync(customerId) });

    [HttpPost("customers/{customerId}/interactions")]
    [Authorize(Policy = "Permission:crm.write")]
    public async Task<IActionResult> LogInteraction(string customerId, [FromBody] LogInteractionDto dto)
        => Ok(new { data = await service.LogInteractionAsync(customerId, dto, UserId) });

    // ── Tasks ──
    [HttpGet("tasks")]
    [Authorize(Policy = "Permission:crm.read.own")]
    public async Task<IActionResult> Tasks([FromQuery] TaskFilterParams filter)
    {
        var r = await service.GetTasksAsync(filter);
        return Ok(new { data = r.Items, total = r.Total, openCount = r.OpenCount, overdueCount = r.OverdueCount });
    }
    [HttpPost("tasks")]
    [Authorize(Policy = "Permission:crm.write")]
    public async Task<IActionResult> CreateTask([FromBody] CreateTaskDto dto) => Ok(new { data = await service.CreateTaskAsync(dto, UserId) });
    [HttpPost("tasks/{id}/complete")]
    [Authorize(Policy = "Permission:crm.write")]
    public async Task<IActionResult> CompleteTask(string id) => Ok(new { data = await service.CompleteTaskAsync(id, UserId) });
    [HttpPost("tasks/{id}/cancel")]
    [Authorize(Policy = "Permission:crm.write")]
    public async Task<IActionResult> CancelTask(string id) => Ok(new { data = await service.CancelTaskAsync(id, UserId) });

    // ── Visits ──
    [HttpGet("visits")]
    [Authorize(Policy = "Permission:crm.read.own")]
    public async Task<IActionResult> Visits([FromQuery] string? customerId, [FromQuery] string? employeeId)
        => Ok(new { data = await service.GetVisitsAsync(customerId, employeeId) });
    [HttpPost("visits")]
    [Authorize(Policy = "Permission:crm.write")]
    public async Task<IActionResult> LogVisit([FromBody] LogVisitDto dto) => Ok(new { data = await service.LogVisitAsync(dto, UserId) });

    // ── Visit targets ──
    [HttpGet("visit-targets")]
    [Authorize(Policy = "Permission:crm.read.own")]
    public async Task<IActionResult> VisitTargets() => Ok(new { data = await service.GetVisitTargetsAsync() });
    [HttpPost("visit-targets")]
    [Authorize(Policy = "Permission:crm.write")]
    public async Task<IActionResult> SaveVisitTarget([FromBody] SaveVisitTargetDto dto) => Ok(new { data = await service.SaveVisitTargetAsync(dto, UserId) });

    // ── Daily activity log ──
    [HttpGet("activity-log")]
    [Authorize(Policy = "Permission:crm.read.own")]
    public async Task<IActionResult> DailyLog([FromQuery] string employeeId, [FromQuery] DateTime? from, [FromQuery] DateTime? to)
        => Ok(new { data = await service.GetDailyActivityAsync(employeeId, from, to) });
    [HttpPost("activity-log")]
    [Authorize(Policy = "Permission:crm.write")]
    public async Task<IActionResult> LogDaily([FromBody] LogDailyActivityDto dto) => Ok(new { data = await service.LogDailyActivityAsync(dto, UserId) });
}
