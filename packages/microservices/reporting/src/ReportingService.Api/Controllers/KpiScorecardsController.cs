using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReportingService.Core.DTOs;
using ReportingService.Core.Entities;
using ReportingService.Core.Services;
using ReportingService.Core.Enums;
using ReportingService.Core.Interfaces.Services;
using ReportingService.Infrastructure.Services;

namespace ReportingService.Api.Controllers;

// RPT-010: a threshold-comparison view over an existing DataSource — no new time-series
// aggregation, just "is this metric's current value on target, in warning, or critical".
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/kpi-scorecards")]
public class KpiScorecardsController(
    IReportingCrudService<KpiScorecard> scorecards,
    IReportingCrudService<DataSource> dataSources,
    MetricResolverService resolver) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "reports.view")]
    public async Task<IActionResult> GetAll()
    {
        var all = await scorecards.GetAllAsync();
        var names = (await dataSources.GetAllAsync()).ToDictionary(s => s.Id, s => s.Name);
        return Ok(new ApiResponse<IEnumerable<KpiScorecardReadDto>> { Success = true, Data = all.OrderBy(s => s.Name).Select(s => ToReadDto(s, names)), StatusCode = 200 });
    }

    [HttpGet("{id}")]
    [Authorize(Policy = "reports.view")]
    public async Task<IActionResult> GetById(string id)
    {
        var s = await scorecards.GetByIdAsync(id);
        if (s == null) return NotFound(new ApiResponse<object> { Success = false, Message = "Scorecard not found.", StatusCode = 404 });
        var names = (await dataSources.GetAllAsync()).ToDictionary(x => x.Id, x => x.Name);
        return Ok(new ApiResponse<KpiScorecardReadDto> { Success = true, Data = ToReadDto(s, names), StatusCode = 200 });
    }

    [HttpPost]
    [Authorize(Policy = "reports.schedule")]
    public async Task<IActionResult> Create([FromBody] CreateKpiScorecardDto dto)
    {
        var dataSource = await dataSources.GetByIdAsync(dto.DataSourceId);
        if (dataSource == null) return NotFound(new ApiResponse<object> { Success = false, Message = "Data source not found.", StatusCode = 404 });

        var scorecard = new KpiScorecard
        {
            Name = dto.Name,
            DataSourceId = dto.DataSourceId,
            TargetValue = dto.TargetValue,
            WarningThreshold = dto.WarningThreshold,
            CriticalThreshold = dto.CriticalThreshold,
            Period = dto.Period,
        };
        await scorecards.CreateAsync(scorecard);
        var names = new Dictionary<string, string> { [dataSource.Id] = dataSource.Name };
        return CreatedAtAction(nameof(GetById), new { id = scorecard.Id, version = "1" },
            new ApiResponse<KpiScorecardReadDto> { Success = true, Message = "Scorecard created.", Data = ToReadDto(scorecard, names), StatusCode = 200 });
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "reports.schedule")]
    public async Task<IActionResult> Delete(string id)
    {
        var ok = await scorecards.DeleteAsync(id);
        if (!ok) return NotFound(new ApiResponse<object> { Success = false, Message = "Scorecard not found.", StatusCode = 404 });
        return Ok(new ApiResponse<object> { Success = true, Message = "Scorecard deleted.", StatusCode = 200 });
    }

    [HttpGet("{id}/status")]
    [Authorize(Policy = "reports.view")]
    public async Task<IActionResult> GetStatus(string id)
    {
        var s = await scorecards.GetByIdAsync(id);
        if (s == null) return NotFound(new ApiResponse<object> { Success = false, Message = "Scorecard not found.", StatusCode = 404 });

        var dataSource = await dataSources.GetByIdAsync(s.DataSourceId);
        if (dataSource == null)
            return Ok(new ApiResponse<KpiScorecardStatusDto> { Success = true, Data = new KpiScorecardStatusDto { ScorecardId = s.Id, Name = s.Name, TargetValue = s.TargetValue, Error = "Data source not found." }, StatusCode = 200 });

        var result = new KpiScorecardStatusDto { ScorecardId = s.Id, Name = s.Name, TargetValue = s.TargetValue };
        try
        {
            result.CurrentValue = await resolver.ResolveAsync(HttpContext.RequestServices, dataSource.MetricKey);
            result.Variance = KpiScorecardStatusRules.Variance(s, result.CurrentValue);
            result.Status = KpiScorecardStatusRules.Resolve(s, result.CurrentValue);
        }
        catch (Exception ex)
        {
            result.Error = ex.Message;
        }

        return Ok(new ApiResponse<KpiScorecardStatusDto> { Success = true, Data = result, StatusCode = 200 });
    }

    private static KpiScorecardReadDto ToReadDto(KpiScorecard s, Dictionary<string, string> names)
    {
        names.TryGetValue(s.DataSourceId, out var name);
        return new KpiScorecardReadDto
        {
            Id = s.Id, Name = s.Name, DataSourceId = s.DataSourceId, DataSourceName = name,
            TargetValue = s.TargetValue, WarningThreshold = s.WarningThreshold, CriticalThreshold = s.CriticalThreshold, Period = s.Period,
        };
    }
}
