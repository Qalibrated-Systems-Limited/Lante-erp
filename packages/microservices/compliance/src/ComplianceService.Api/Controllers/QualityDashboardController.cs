using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ComplianceService.Core.DTOs.Common;
using ComplianceService.Core.DTOs.Dashboard;
using ComplianceService.Core.Interfaces.Services;

namespace ComplianceService.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/quality-dashboard")]
public class QualityDashboardController(IQualityDashboardService dashboard) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "compliance.read")]
    public async Task<ActionResult<ApiResponse<QualityDashboardDto>>> Get()
    {
        var result = await dashboard.ComputeAsync();
        return Ok(ApiResponse<QualityDashboardDto>.Ok(result));
    }
}
