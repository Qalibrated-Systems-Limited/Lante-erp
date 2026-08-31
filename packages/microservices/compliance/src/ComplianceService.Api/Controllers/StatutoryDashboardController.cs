using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ComplianceService.Core.DTOs.Common;
using ComplianceService.Core.DTOs.Statutory;
using ComplianceService.Core.Interfaces.Services;

namespace ComplianceService.Api.Controllers;

// STAT-010: one-screen view of all upcoming statutory deadlines sorted by due date — GREEN
// (>60 days), AMBER (30-60 days), RED (<30 days or overdue).
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/statutory-dashboard")]
public class StatutoryDashboardController(IStatutoryDashboardService dashboard) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "statutory.read")]
    public async Task<ActionResult<ApiResponse<StatutoryDashboardDto>>> Get()
    {
        var data = await dashboard.ComputeAsync();
        return Ok(ApiResponse<StatutoryDashboardDto>.Ok(data));
    }
}
