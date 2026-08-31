using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HSEService.Core.DTOs.Common;
using HSEService.Core.DTOs.Dashboard;
using HSEService.Core.Interfaces.Services;

namespace HSEService.Api.Controllers;

// HSE-008: HSE KPI dashboard — TRIR, LTIF, near-miss frequency, open corrective actions,
// computed live from current data (no cached snapshot).
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/hse-dashboard")]
public class HseDashboardController(IHseDashboardService dashboard) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "hse.read")]
    public async Task<ActionResult<ApiResponse<HseDashboardDto>>> Get([FromQuery] decimal hoursWorkedYtd = 0)
    {
        var kpis = await dashboard.ComputeAsync(hoursWorkedYtd);
        return Ok(ApiResponse<HseDashboardDto>.Ok(kpis));
    }
}
