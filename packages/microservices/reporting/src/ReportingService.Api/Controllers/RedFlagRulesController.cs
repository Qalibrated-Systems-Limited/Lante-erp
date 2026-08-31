using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReportingService.Core.DTOs;
using ReportingService.Core.Entities;
using ReportingService.Core.Interfaces.Services;

namespace ReportingService.Api.Controllers;

// RPT-011/012: rules are evaluated by RedFlagEvaluationBackgroundService, not on-demand here —
// this controller is CRUD over rules plus a read-only view of the events they've raised.
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/red-flag-rules")]
public class RedFlagRulesController(
    IReportingCrudService<RedFlagRule> rules,
    IReportingCrudService<RedFlagEvent> events,
    IReportingCrudService<DataSource> dataSources) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "reports.view")]
    public async Task<IActionResult> GetAll()
    {
        var all = await rules.GetAllAsync();
        var names = (await dataSources.GetAllAsync()).ToDictionary(s => s.Id, s => s.Name);
        return Ok(new ApiResponse<IEnumerable<RedFlagRuleReadDto>> { Success = true, Data = all.OrderBy(r => r.Name).Select(r => ToReadDto(r, names)), StatusCode = 200 });
    }

    [HttpGet("{id}")]
    [Authorize(Policy = "reports.view")]
    public async Task<IActionResult> GetById(string id)
    {
        var r = await rules.GetByIdAsync(id);
        if (r == null) return NotFound(new ApiResponse<object> { Success = false, Message = "Red-flag rule not found.", StatusCode = 404 });
        var names = (await dataSources.GetAllAsync()).ToDictionary(s => s.Id, s => s.Name);
        return Ok(new ApiResponse<RedFlagRuleReadDto> { Success = true, Data = ToReadDto(r, names), StatusCode = 200 });
    }

    [HttpPost]
    [Authorize(Policy = "reports.schedule")]
    public async Task<IActionResult> Create([FromBody] CreateRedFlagRuleDto dto)
    {
        var dataSource = await dataSources.GetByIdAsync(dto.DataSourceId);
        if (dataSource == null) return NotFound(new ApiResponse<object> { Success = false, Message = "Data source not found.", StatusCode = 404 });

        var rule = new RedFlagRule
        {
            Name = dto.Name,
            DataSourceId = dto.DataSourceId,
            Condition = dto.Condition,
            ThresholdValue = dto.ThresholdValue,
            Severity = dto.Severity,
            IsActive = dto.IsActive,
        };
        await rules.CreateAsync(rule);
        var names = new Dictionary<string, string> { [dataSource.Id] = dataSource.Name };
        return CreatedAtAction(nameof(GetById), new { id = rule.Id, version = "1" },
            new ApiResponse<RedFlagRuleReadDto> { Success = true, Message = "Red-flag rule created.", Data = ToReadDto(rule, names), StatusCode = 200 });
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "reports.schedule")]
    public async Task<IActionResult> Delete(string id)
    {
        var ok = await rules.DeleteAsync(id);
        if (!ok) return NotFound(new ApiResponse<object> { Success = false, Message = "Red-flag rule not found.", StatusCode = 404 });
        return Ok(new ApiResponse<object> { Success = true, Message = "Red-flag rule deleted.", StatusCode = 200 });
    }

    [HttpGet("{id}/events")]
    [Authorize(Policy = "reports.view")]
    public async Task<IActionResult> GetEvents(string id)
    {
        var rule = await rules.GetByIdAsync(id);
        if (rule == null) return NotFound(new ApiResponse<object> { Success = false, Message = "Red-flag rule not found.", StatusCode = 404 });

        var list = await events.FindAsync(e => e.RedFlagRuleId == id);
        return Ok(new ApiResponse<IEnumerable<RedFlagEventReadDto>> { Success = true, Data = list.OrderByDescending(e => e.TriggeredAt).Select(e => ToEventDto(e, rule.Name)), StatusCode = 200 });
    }

    [HttpGet("events/open")]
    [Authorize(Policy = "reports.view")]
    public async Task<IActionResult> GetOpenEvents()
    {
        var open = await events.FindAsync(e => e.Status == Core.Enums.RedFlagEventStatus.Open);
        var names = (await rules.GetAllAsync()).ToDictionary(r => r.Id, r => r.Name);
        return Ok(new ApiResponse<IEnumerable<RedFlagEventReadDto>> { Success = true, Data = open.OrderByDescending(e => e.TriggeredAt).Select(e => ToEventDto(e, names.GetValueOrDefault(e.RedFlagRuleId))), StatusCode = 200 });
    }

    private static RedFlagRuleReadDto ToReadDto(RedFlagRule r, Dictionary<string, string> names)
    {
        names.TryGetValue(r.DataSourceId, out var name);
        return new RedFlagRuleReadDto
        {
            Id = r.Id, Name = r.Name, DataSourceId = r.DataSourceId, DataSourceName = name,
            Condition = r.Condition, ThresholdValue = r.ThresholdValue, Severity = r.Severity, IsActive = r.IsActive,
        };
    }

    private static RedFlagEventReadDto ToEventDto(RedFlagEvent e, string? ruleName) => new()
    {
        Id = e.Id, RedFlagRuleId = e.RedFlagRuleId, RuleName = ruleName, TriggeredAt = e.TriggeredAt,
        Severity = e.Severity, Value = e.Value, DaysOpen = e.DaysOpen, ResolvedAt = e.ResolvedAt, Status = e.Status,
    };
}
