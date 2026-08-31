using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CrmService.Core.DTOs.Dashboards;
using CrmService.Core.Interfaces.Services;

namespace CrmService.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/dashboard")]
[Authorize]
public class DashboardController(IDashboardService service) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    [HttpGet("md-pipeline")]
    [Authorize(Policy = "Permission:crm.read.own")]
    public async Task<IActionResult> MdPipeline() => Ok(new { data = await service.GetMdPipelineAsync() });

    [HttpGet("se-performance")]
    [Authorize(Policy = "Permission:crm.read.own")]
    public async Task<IActionResult> SePerformance([FromQuery] string? period) => Ok(new { data = await service.GetSePerformanceAsync(period) });

    [HttpGet("targets")]
    [Authorize(Policy = "Permission:crm.read.own")]
    public async Task<IActionResult> Targets() => Ok(new { data = await service.GetTargetsAsync() });

    [HttpPost("targets")]
    [Authorize(Policy = "Permission:crm.approve.bd")]
    public async Task<IActionResult> SaveTarget([FromBody] SaveSalesTargetDto dto) => Ok(new { data = await service.SaveTargetAsync(dto, UserId) });
}
