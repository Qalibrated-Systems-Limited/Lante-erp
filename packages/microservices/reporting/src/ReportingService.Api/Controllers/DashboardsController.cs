using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReportingService.Core.DTOs;
using ReportingService.Core.Entities;
using ReportingService.Core.Interfaces.Services;
using ReportingService.Infrastructure.Services;

namespace ReportingService.Api.Controllers;

// RPT-008/009: named dashboards containing a fixed grid of widgets, each bound to a DataSource.
// No drag-drop layout builder — Position is a plain sort order.
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/dashboards")]
public class DashboardsController(
    IReportingCrudService<Dashboard> dashboards,
    IReportingCrudService<DashboardWidget> widgets,
    IReportingCrudService<DataSource> dataSources,
    MetricResolverService resolver) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "reports.view")]
    public async Task<IActionResult> GetAll()
    {
        var all = await dashboards.GetAllAsync();
        return Ok(new ApiResponse<IEnumerable<DashboardReadDto>> { Success = true, Data = all.OrderBy(d => d.Name).Select(ToReadDto), StatusCode = 200 });
    }

    [HttpGet("{id}")]
    [Authorize(Policy = "reports.view")]
    public async Task<IActionResult> GetById(string id)
    {
        var d = await dashboards.GetByIdAsync(id);
        if (d == null) return NotFound(new ApiResponse<object> { Success = false, Message = "Dashboard not found.", StatusCode = 404 });
        return Ok(new ApiResponse<DashboardReadDto> { Success = true, Data = ToReadDto(d), StatusCode = 200 });
    }

    [HttpPost]
    [Authorize(Policy = "reports.schedule")]
    public async Task<IActionResult> Create([FromBody] CreateDashboardDto dto)
    {
        var dashboard = new Dashboard { Name = dto.Name, Description = dto.Description, IsDefault = dto.IsDefault, OwnerUserId = dto.OwnerUserId };
        await dashboards.CreateAsync(dashboard);
        return CreatedAtAction(nameof(GetById), new { id = dashboard.Id, version = "1" },
            new ApiResponse<DashboardReadDto> { Success = true, Message = "Dashboard created.", Data = ToReadDto(dashboard), StatusCode = 200 });
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "reports.schedule")]
    public async Task<IActionResult> Delete(string id)
    {
        var ok = await dashboards.DeleteAsync(id);
        if (!ok) return NotFound(new ApiResponse<object> { Success = false, Message = "Dashboard not found.", StatusCode = 404 });
        return Ok(new ApiResponse<object> { Success = true, Message = "Dashboard deleted.", StatusCode = 200 });
    }

    [HttpGet("{id}/widgets")]
    [Authorize(Policy = "reports.view")]
    public async Task<IActionResult> GetWidgets(string id)
    {
        var list = await widgets.FindAsync(w => w.DashboardId == id);
        var names = (await dataSources.GetAllAsync()).ToDictionary(s => s.Id, s => s.Name);
        return Ok(new ApiResponse<IEnumerable<DashboardWidgetReadDto>> { Success = true, Data = list.OrderBy(w => w.Position).Select(w => ToWidgetDto(w, names)), StatusCode = 200 });
    }

    [HttpPost("{id}/widgets")]
    [Authorize(Policy = "reports.schedule")]
    public async Task<IActionResult> AddWidget(string id, [FromBody] CreateDashboardWidgetDto dto)
    {
        var dashboard = await dashboards.GetByIdAsync(id);
        if (dashboard == null) return NotFound(new ApiResponse<object> { Success = false, Message = "Dashboard not found.", StatusCode = 404 });
        var dataSource = await dataSources.GetByIdAsync(dto.DataSourceId);
        if (dataSource == null) return NotFound(new ApiResponse<object> { Success = false, Message = "Data source not found.", StatusCode = 404 });

        var widget = new DashboardWidget
        {
            DashboardId = id,
            DataSourceId = dto.DataSourceId,
            Title = dto.Title,
            WidgetType = dto.WidgetType,
            Position = dto.Position,
            RefreshIntervalSeconds = dto.RefreshIntervalSeconds,
        };
        await widgets.CreateAsync(widget);
        var names = new Dictionary<string, string> { [dataSource.Id] = dataSource.Name };
        return Ok(new ApiResponse<DashboardWidgetReadDto> { Success = true, Message = "Widget added.", Data = ToWidgetDto(widget, names), StatusCode = 200 });
    }

    [HttpDelete("{id}/widgets/{widgetId}")]
    [Authorize(Policy = "reports.schedule")]
    public async Task<IActionResult> RemoveWidget(string id, string widgetId)
    {
        var widget = await widgets.GetByIdAsync(widgetId);
        if (widget == null || widget.DashboardId != id)
            return NotFound(new ApiResponse<object> { Success = false, Message = "Widget not found.", StatusCode = 404 });

        await widgets.DeleteAsync(widgetId);
        return Ok(new ApiResponse<object> { Success = true, Message = "Widget removed.", StatusCode = 200 });
    }

    // Aggregator: resolves every widget's current value in one round trip. One widget's failed
    // resolution (upstream service down) doesn't sink the others — same resilience convention as
    // ReportHelpers.SafeCallAsync used throughout ReportsController.
    [HttpGet("{id}/data")]
    [Authorize(Policy = "reports.view")]
    public async Task<IActionResult> GetData(string id)
    {
        var dashboard = await dashboards.GetByIdAsync(id);
        if (dashboard == null) return NotFound(new ApiResponse<object> { Success = false, Message = "Dashboard not found.", StatusCode = 404 });

        var widgetList = (await widgets.FindAsync(w => w.DashboardId == id)).OrderBy(w => w.Position).ToList();
        var dataSourceLookup = (await dataSources.GetAllAsync()).ToDictionary(s => s.Id, s => s);

        var values = new List<DashboardWidgetValueDto>();
        foreach (var widget in widgetList)
        {
            var result = new DashboardWidgetValueDto { WidgetId = widget.Id, Title = widget.Title, WidgetType = widget.WidgetType, Position = widget.Position };
            if (!dataSourceLookup.TryGetValue(widget.DataSourceId, out var dataSource))
            {
                result.Error = "Data source not found.";
            }
            else
            {
                try { result.Value = await resolver.ResolveAsync(HttpContext.RequestServices, dataSource.MetricKey); }
                catch (Exception ex) { result.Error = ex.Message; }
            }
            values.Add(result);
        }

        return Ok(new ApiResponse<DashboardDataDto> { Success = true, Data = new DashboardDataDto { DashboardId = id, Widgets = values }, StatusCode = 200 });
    }

    private static DashboardReadDto ToReadDto(Dashboard d) => new()
    {
        Id = d.Id, Name = d.Name, Description = d.Description, IsDefault = d.IsDefault, OwnerUserId = d.OwnerUserId,
    };

    private static DashboardWidgetReadDto ToWidgetDto(DashboardWidget w, Dictionary<string, string> names)
    {
        names.TryGetValue(w.DataSourceId, out var name);
        return new DashboardWidgetReadDto
        {
            Id = w.Id, DashboardId = w.DashboardId, DataSourceId = w.DataSourceId, DataSourceName = name,
            Title = w.Title, WidgetType = w.WidgetType, Position = w.Position, RefreshIntervalSeconds = w.RefreshIntervalSeconds,
        };
    }
}
