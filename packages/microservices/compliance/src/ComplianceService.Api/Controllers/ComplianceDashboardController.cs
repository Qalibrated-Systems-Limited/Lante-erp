using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ComplianceService.Core.DTOs.Common;
using ComplianceService.Core.DTOs.Dashboard;
using ComplianceService.Core.Interfaces.Services;

namespace ComplianceService.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/compliance-dashboard")]
public class ComplianceDashboardController(IComplianceDashboardService dashboard) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "compliance.read")]
    public async Task<ActionResult<ApiResponse<ComplianceDashboardDto>>> Get()
    {
        var kpis = await dashboard.ComputeAsync();
        return Ok(ApiResponse<ComplianceDashboardDto>.Ok(kpis));
    }
}
