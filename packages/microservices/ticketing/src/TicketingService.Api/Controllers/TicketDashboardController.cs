using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TicketingService.Core.DTOs.Common;
using TicketingService.Core.DTOs.Satisfaction;
using TicketingService.Core.DTOs.SLA;
using TicketingService.Core.Interfaces.Services;

namespace TicketingService.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/tickets/dashboard")]
public class TicketDashboardController(
    ITicketService ticketService,
    ISLAService slaService,
    ISatisfactionService satisfactionService) : ControllerBase
{
    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    private (bool canReadAll, string? deptId) GetTicketScope()
    {
        var permissions = User.Claims.Where(c => c.Type == "permission").Select(c => c.Value).ToHashSet();
        bool canReadAll = permissions.Contains("system.admin") || permissions.Contains("tickets.read.all");
        string? deptId  = canReadAll ? null : User.FindFirstValue("department_id");
        return (canReadAll, deptId);
    }

    [HttpGet("summary")]
    [Authorize(Policy = "tickets.read.own")]
    public async Task<ActionResult<ApiResponse<object>>> GetSummary()
    {
        var (_, deptId) = GetTicketScope();
        var summary = await ticketService.GetDashboardSummaryAsync(deptId);
        return Ok(ApiResponse<object>.Ok(summary));
    }

    [HttpGet("sla-compliance")]
    [Authorize(Policy = "tickets.read.own")]
    public async Task<ActionResult<ApiResponse<SLASummaryDto>>> GetSLACompliance()
    {
        var (_, deptId) = GetTicketScope();
        var summary = await slaService.GetSLASummaryAsync(deptId);
        return Ok(ApiResponse<SLASummaryDto>.Ok(summary));
    }

    [HttpGet("my-summary")]
    [Authorize(Policy = "tickets.read.own")]
    public async Task<ActionResult<ApiResponse<object>>> GetMySummary()
    {
        var summary = await ticketService.GetMySummaryAsync(CurrentUserId);
        return Ok(ApiResponse<object>.Ok(summary));
    }

    // #14 — aggregate customer-satisfaction report.
    [HttpGet("csat")]
    [Authorize(Policy = "tickets.read.own")]
    public async Task<ActionResult<ApiResponse<CsatSummaryDto>>> GetCsat([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var summary = await satisfactionService.GetCsatSummaryAsync(from, to);
        return Ok(ApiResponse<CsatSummaryDto>.Ok(summary));
    }

    // D6-3 — customer-service dashboard (open-by-category+aging, SLA compliance, avg resolution,
    // avg satisfaction, survey response rate).
    [HttpGet("cs-overview")]
    [Authorize(Policy = "tickets.read.own")]
    public async Task<ActionResult<ApiResponse<CsDashboardDto>>> GetCsOverview([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var dash = await satisfactionService.GetCsDashboardAsync(from, to);
        return Ok(ApiResponse<CsDashboardDto>.Ok(dash));
    }
}
