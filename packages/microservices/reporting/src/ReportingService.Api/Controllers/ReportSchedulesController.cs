using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Cronos;
using ReportingService.Core.DTOs;
using ReportingService.Core.Entities;
using ReportingService.Core.Interfaces.Services;

namespace ReportingService.Api.Controllers;

// RPT-002/003/004: recurring delivery of a ReportDefinition, plus its recipients.
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/report-schedules")]
public class ReportSchedulesController(
    IReportingCrudService<ReportSchedule> schedules,
    IReportingCrudService<ReportRecipient> recipients,
    IReportingCrudService<ReportDefinition> definitions) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "reports.view")]
    public async Task<IActionResult> GetAll([FromQuery] string? reportDefinitionId)
    {
        var all = reportDefinitionId != null
            ? await schedules.FindAsync(s => s.ReportDefinitionId == reportDefinitionId)
            : (await schedules.GetAllAsync()).ToList();
        var names = (await definitions.GetAllAsync()).ToDictionary(d => d.Id, d => d.Name);
        return Ok(new ApiResponse<IEnumerable<ReportScheduleReadDto>> { Success = true, Data = all.Select(s => ToReadDto(s, names)), StatusCode = 200 });
    }

    [HttpGet("{id}")]
    [Authorize(Policy = "reports.view")]
    public async Task<IActionResult> GetById(string id)
    {
        var s = await schedules.GetByIdAsync(id);
        if (s == null) return NotFound(new ApiResponse<object> { Success = false, Message = "Schedule not found.", StatusCode = 404 });
        var names = (await definitions.GetAllAsync()).ToDictionary(d => d.Id, d => d.Name);
        return Ok(new ApiResponse<ReportScheduleReadDto> { Success = true, Data = ToReadDto(s, names), StatusCode = 200 });
    }

    [HttpPost]
    [Authorize(Policy = "reports.schedule")]
    public async Task<IActionResult> Create([FromBody] CreateReportScheduleDto dto)
    {
        var definition = await definitions.GetByIdAsync(dto.ReportDefinitionId);
        if (definition == null) return NotFound(new ApiResponse<object> { Success = false, Message = "Report definition not found.", StatusCode = 404 });

        CronExpression cron;
        try { cron = CronExpression.Parse(dto.CronExpression); }
        catch (Exception ex) { return BadRequest(new ApiResponse<object> { Success = false, Message = $"Invalid cron expression: {ex.Message}", StatusCode = 400 }); }

        var schedule = new ReportSchedule
        {
            ReportDefinitionId = dto.ReportDefinitionId,
            CronExpression = dto.CronExpression,
            Format = dto.Format,
            NextRunAt = cron.GetNextOccurrence(DateTime.UtcNow, TimeZoneInfo.Utc),
        };
        await schedules.CreateAsync(schedule);
        var names = new Dictionary<string, string> { [definition.Id] = definition.Name };
        return CreatedAtAction(nameof(GetById), new { id = schedule.Id, version = "1" },
            new ApiResponse<ReportScheduleReadDto> { Success = true, Message = "Schedule created.", Data = ToReadDto(schedule, names), StatusCode = 200 });
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "reports.schedule")]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateReportScheduleDto dto)
    {
        var schedule = await schedules.GetByIdAsync(id);
        if (schedule == null) return NotFound(new ApiResponse<object> { Success = false, Message = "Schedule not found.", StatusCode = 404 });

        CronExpression cron;
        try { cron = CronExpression.Parse(dto.CronExpression); }
        catch (Exception ex) { return BadRequest(new ApiResponse<object> { Success = false, Message = $"Invalid cron expression: {ex.Message}", StatusCode = 400 }); }

        schedule.CronExpression = dto.CronExpression;
        schedule.Format = dto.Format;
        schedule.IsActive = dto.IsActive;
        schedule.NextRunAt = dto.IsActive ? cron.GetNextOccurrence(DateTime.UtcNow, TimeZoneInfo.Utc) : null;
        await schedules.UpdateAsync(schedule);

        var names = (await definitions.GetAllAsync()).ToDictionary(d => d.Id, d => d.Name);
        return Ok(new ApiResponse<ReportScheduleReadDto> { Success = true, Message = "Schedule updated.", Data = ToReadDto(schedule, names), StatusCode = 200 });
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "reports.schedule")]
    public async Task<IActionResult> Delete(string id)
    {
        var ok = await schedules.DeleteAsync(id);
        if (!ok) return NotFound(new ApiResponse<object> { Success = false, Message = "Schedule not found.", StatusCode = 404 });
        return Ok(new ApiResponse<object> { Success = true, Message = "Schedule deleted.", StatusCode = 200 });
    }

    [HttpGet("{id}/recipients")]
    [Authorize(Policy = "reports.view")]
    public async Task<IActionResult> GetRecipients(string id)
    {
        var list = await recipients.FindAsync(r => r.ReportScheduleId == id);
        return Ok(new ApiResponse<IEnumerable<ReportRecipientReadDto>> { Success = true, Data = list.Select(ToRecipientDto), StatusCode = 200 });
    }

    [HttpPost("{id}/recipients")]
    [Authorize(Policy = "reports.schedule")]
    public async Task<IActionResult> AddRecipient(string id, [FromBody] CreateReportRecipientDto dto)
    {
        var schedule = await schedules.GetByIdAsync(id);
        if (schedule == null) return NotFound(new ApiResponse<object> { Success = false, Message = "Schedule not found.", StatusCode = 404 });

        var recipient = new ReportRecipient { ReportScheduleId = id, Email = dto.Email, UserId = dto.UserId };
        await recipients.CreateAsync(recipient);
        return Ok(new ApiResponse<ReportRecipientReadDto> { Success = true, Message = "Recipient added.", Data = ToRecipientDto(recipient), StatusCode = 200 });
    }

    [HttpDelete("{id}/recipients/{recipientId}")]
    [Authorize(Policy = "reports.schedule")]
    public async Task<IActionResult> RemoveRecipient(string id, string recipientId)
    {
        var recipient = await recipients.GetByIdAsync(recipientId);
        if (recipient == null || recipient.ReportScheduleId != id)
            return NotFound(new ApiResponse<object> { Success = false, Message = "Recipient not found.", StatusCode = 404 });

        await recipients.DeleteAsync(recipientId);
        return Ok(new ApiResponse<object> { Success = true, Message = "Recipient removed.", StatusCode = 200 });
    }

    private static ReportScheduleReadDto ToReadDto(ReportSchedule s, Dictionary<string, string> names)
    {
        names.TryGetValue(s.ReportDefinitionId, out var name);
        return new ReportScheduleReadDto
        {
            Id = s.Id, ReportDefinitionId = s.ReportDefinitionId, ReportDefinitionName = name,
            CronExpression = s.CronExpression, Format = s.Format, IsActive = s.IsActive,
            LastRunAt = s.LastRunAt, NextRunAt = s.NextRunAt,
        };
    }

    private static ReportRecipientReadDto ToRecipientDto(ReportRecipient r) => new()
    {
        Id = r.Id, ReportScheduleId = r.ReportScheduleId, Email = r.Email, UserId = r.UserId, IsActive = r.IsActive,
    };
}
