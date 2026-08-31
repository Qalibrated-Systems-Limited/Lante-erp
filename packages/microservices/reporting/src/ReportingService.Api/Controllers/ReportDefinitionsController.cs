using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReportingService.Core.DTOs;
using ReportingService.Core.Entities;
using ReportingService.Core.Enums;
using ReportingService.Core.Interfaces.Services;
using ReportingService.Infrastructure.Data;
using ReportingService.Infrastructure.Services;

namespace ReportingService.Api.Controllers;

// RPT-001: the catalog of reports ReportsController already knows how to build, plus a manual
// "run now" action for ad-hoc delivery outside of any schedule.
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/report-definitions")]
[Authorize(Policy = "reports.view")]
public class ReportDefinitionsController(
    IReportingCrudService<ReportDefinition> definitions,
    ReportGenerationService generator,
    ReportingDbContext db,
    IConfiguration config,
    ILogger<ReportDefinitionsController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var all = await definitions.GetAllAsync();
        return Ok(new ApiResponse<IEnumerable<ReportDefinitionReadDto>> { Success = true, Data = all.OrderBy(d => d.Name).Select(ToReadDto), StatusCode = 200 });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        var d = await definitions.GetByIdAsync(id);
        if (d == null) return NotFound(new ApiResponse<object> { Success = false, Message = "Report definition not found.", StatusCode = 404 });
        return Ok(new ApiResponse<ReportDefinitionReadDto> { Success = true, Data = ToReadDto(d), StatusCode = 200 });
    }

    // Ad-hoc generation — no schedule involved, no email delivery, just a downloadable ReportRun.
    // Runs under the calling user's own JWT (already on the ambient HttpContext), unlike the
    // scheduler which has to impersonate a system identity.
    [HttpPost("{id}/run-now")]
    [Authorize(Policy = "reports.schedule")]
    public async Task<IActionResult> RunNow(string id, [FromQuery] ReportFormat format = ReportFormat.Excel)
    {
        var definition = await definitions.GetByIdAsync(id);
        if (definition == null) return NotFound(new ApiResponse<object> { Success = false, Message = "Report definition not found.", StatusCode = 404 });

        var run = new Core.Entities.ReportRun
        {
            ReportDefinitionId = definition.Id,
            Format = format,
            TriggeredBy = User.Identity?.Name ?? "manual",
        };

        // File-path/delivery grouping only — the DbContext's own connection is already scoped to
        // the right tenant by TenantDbConnectionInterceptor regardless of this value.
        var schema = User.FindFirst("schema")?.Value ?? "public";
        var result = await generator.GenerateAsync(HttpContext.RequestServices, schema, definition.Key, run.Id, format);
        run.Status = result.Success ? RunStatus.Success : RunStatus.Failed;
        run.FileUrl = result.FileUrl;
        run.ErrorMessage = result.ErrorMessage;

        db.ReportRuns.Add(run);
        await db.SaveChangesAsync();

        if (!result.Success)
            logger.LogWarning("Manual run of {Key} failed: {Error}", definition.Key, result.ErrorMessage);

        var names = new Dictionary<string, string> { [definition.Id] = definition.Name };
        return Ok(new ApiResponse<ReportRunReadDto> { Success = result.Success, Message = result.ErrorMessage, Data = ReportRunsController.ToReadDto(run, names, config), StatusCode = result.Success ? 200 : 502 });
    }

    private static ReportDefinitionReadDto ToReadDto(ReportDefinition d) => new()
    {
        Id = d.Id, Key = d.Key, Name = d.Name, Description = d.Description, Category = d.Category, IsActive = d.IsActive,
    };
}
