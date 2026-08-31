using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReportingService.Core.DTOs;
using ReportingService.Core.Entities;
using ReportingService.Core.Interfaces.Services;
using ReportingService.Infrastructure.Services;

namespace ReportingService.Api.Controllers;

// RPT-006/007: the catalog of resolvable metrics — what Dashboards/KpiScorecards/RedFlagRules
// (Phases 3-5) bind to.
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/data-sources")]
[Authorize(Policy = "reports.view")]
public class DataSourcesController(
    IReportingCrudService<DataSource> dataSources,
    MetricResolverService resolver) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var all = await dataSources.GetAllAsync();
        return Ok(new ApiResponse<IEnumerable<DataSourceReadDto>> { Success = true, Data = all.OrderBy(d => d.Name).Select(ToReadDto), StatusCode = 200 });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        var d = await dataSources.GetByIdAsync(id);
        if (d == null) return NotFound(new ApiResponse<object> { Success = false, Message = "Data source not found.", StatusCode = 404 });
        return Ok(new ApiResponse<DataSourceReadDto> { Success = true, Data = ToReadDto(d), StatusCode = 200 });
    }

    [HttpGet("{id}/value")]
    public async Task<IActionResult> GetValue(string id)
    {
        var d = await dataSources.GetByIdAsync(id);
        if (d == null) return NotFound(new ApiResponse<object> { Success = false, Message = "Data source not found.", StatusCode = 404 });

        try
        {
            var value = await resolver.ResolveAsync(HttpContext.RequestServices, d.MetricKey);
            return Ok(new ApiResponse<DataSourceValueDto> { Success = true, Data = new DataSourceValueDto { DataSourceId = d.Id, MetricKey = d.MetricKey, Value = value }, StatusCode = 200 });
        }
        catch (Exception ex)
        {
            return StatusCode(502, new ApiResponse<DataSourceValueDto> { Success = false, Message = ex.Message, Data = new DataSourceValueDto { DataSourceId = d.Id, MetricKey = d.MetricKey, Error = ex.Message }, StatusCode = 502 });
        }
    }

    private static DataSourceReadDto ToReadDto(DataSource d) => new()
    {
        Id = d.Id, Name = d.Name, ModuleName = d.ModuleName, MetricKey = d.MetricKey, Description = d.Description,
    };
}
