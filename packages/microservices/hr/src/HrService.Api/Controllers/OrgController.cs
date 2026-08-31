using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HrService.Core.DTOs.Org;
using HrService.Core.Interfaces.Services;

namespace HrService.Api.Controllers;

/// <summary>H1 (HR-DEC-3, HR-005/P32) — job positions, the organisational chart, and read-through to the
/// departments and branches user-service owns.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/hr")]
[Authorize]
public class OrgController(IOrgService service) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    // ── Positions ──
    [HttpGet("positions")]
    [Authorize(Policy = "Permission:hr.read.own")]
    public async Task<IActionResult> ListPositions([FromQuery] bool includeInactive = false)
        => Ok(new { data = await service.ListPositionsAsync(includeInactive) });

    [HttpGet("positions/{id}")]
    [Authorize(Policy = "Permission:hr.read.own")]
    public async Task<IActionResult> GetPosition(string id)
    {
        var p = await service.GetPositionAsync(id);
        return p is null ? NotFound(new { message = "Position not found." }) : Ok(new { data = p });
    }

    [HttpPost("positions")]
    [Authorize(Policy = "Permission:hr.write")]
    public async Task<IActionResult> CreatePosition([FromBody] CreatePositionDto dto)
        => Act(await service.CreatePositionAsync(dto, UserId));

    [HttpPut("positions/{id}")]
    [Authorize(Policy = "Permission:hr.write")]
    public async Task<IActionResult> UpdatePosition(string id, [FromBody] UpdatePositionDto dto)
        => Act(await service.UpdatePositionAsync(id, dto, UserId));

    // ── Org chart (P32 — all staff may view) ──
    [HttpGet("org-chart")]
    [Authorize(Policy = "Permission:hr.read.own")]
    public async Task<IActionResult> GetChart([FromQuery] bool includeInactive = false)
        => Ok(new { data = await service.GetChartAsync(includeInactive) });

    [HttpPut("org-chart/{employeeId}/reporting-line")]
    [Authorize(Policy = "Permission:hr.write")]
    public async Task<IActionResult> SetReportingLine(string employeeId, [FromBody] SetReportingLineDto dto)
        => Act(await service.SetReportingLineAsync(employeeId, dto, UserId));

    /// <summary>Rebuilds every node from the employees' reporting lines — also how pre-existing employees get
    /// their nodes.</summary>
    [HttpPost("org-chart/rebuild")]
    [Authorize(Policy = "Permission:hr.write")]
    public async Task<IActionResult> Rebuild() => Act(await service.RebuildAsync(UserId));

    // ── Directory read-through (user-service owns these) ──
    [HttpGet("departments")]
    [Authorize(Policy = "Permission:hr.read.own")]
    public async Task<IActionResult> Departments() => Ok(new { data = await service.ListDepartmentsAsync() });

    [HttpGet("branches")]
    [Authorize(Policy = "Permission:hr.read.own")]
    public async Task<IActionResult> Branches() => Ok(new { data = await service.ListBranchesAsync() });

    private IActionResult Act(OrgActionResult r)
        => r.Status == "Error" ? BadRequest(new { message = r.Message }) : Ok(new { data = r });
}
