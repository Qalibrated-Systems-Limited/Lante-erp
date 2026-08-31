using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReportingService.Core.DTOs;
using ReportingService.Core.Entities;
using ReportingService.Core.Interfaces.Services;

namespace ReportingService.Api.Controllers;

// RPT-005: run history — both scheduled and ad-hoc ("run now") generations.
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/report-runs")]
[Authorize(Policy = "reports.view")]
public class ReportRunsController(
    IReportingCrudService<ReportRun> runs,
    IReportingCrudService<ReportDefinition> definitions,
    IConfiguration config) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? reportDefinitionId, [FromQuery] string? reportScheduleId, [FromQuery] int limit = 100)
    {
        var all = (await runs.GetAllAsync()).AsEnumerable();
        if (reportDefinitionId != null) all = all.Where(r => r.ReportDefinitionId == reportDefinitionId);
        if (reportScheduleId != null) all = all.Where(r => r.ReportScheduleId == reportScheduleId);

        var names = (await definitions.GetAllAsync()).ToDictionary(d => d.Id, d => d.Name);
        var ordered = all.OrderByDescending(r => r.GeneratedAt).Take(limit).Select(r => ToReadDto(r, names, config));
        return Ok(new ApiResponse<IEnumerable<ReportRunReadDto>> { Success = true, Data = ordered, StatusCode = 200 });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        var r = await runs.GetByIdAsync(id);
        if (r == null) return NotFound(new ApiResponse<object> { Success = false, Message = "Report run not found.", StatusCode = 404 });
        var names = (await definitions.GetAllAsync()).ToDictionary(d => d.Id, d => d.Name);
        return Ok(new ApiResponse<ReportRunReadDto> { Success = true, Data = ToReadDto(r, names, config), StatusCode = 200 });
    }

    internal static ReportRunReadDto ToReadDto(ReportRun r, Dictionary<string, string> names, IConfiguration config)
    {
        names.TryGetValue(r.ReportDefinitionId, out var name);
        var baseUrl = (config["Storage:BaseUrl"] ?? "").TrimEnd('/');
        return new ReportRunReadDto
        {
            Id = r.Id, ReportDefinitionId = r.ReportDefinitionId, ReportDefinitionName = name,
            ReportScheduleId = r.ReportScheduleId, GeneratedAt = r.GeneratedAt, Format = r.Format,
            FileUrl = r.FileUrl == null ? null : $"{baseUrl}{r.FileUrl}", Status = r.Status, ErrorMessage = r.ErrorMessage, TriggeredBy = r.TriggeredBy,
        };
    }
}
