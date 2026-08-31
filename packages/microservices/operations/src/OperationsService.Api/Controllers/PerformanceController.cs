using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OperationsService.Core.DTOs.Common;
using OperationsService.Core.Interfaces.Services;

namespace OperationsService.Api.Controllers;

/// <summary>#339 -- technician performance scorecards. Get* reads whatever Compute* last wrote for a
/// period; it does not compute on demand.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/performance")]
[Authorize]
public class PerformanceController(IPerformanceService performanceService) : ControllerBase
{
    [HttpGet("technicians/{technicianId}")]
    [Authorize(Policy = "Permission:operations.read.dept")]
    public async Task<IActionResult> GetTechnicianMetrics(string technicianId, [FromQuery] int month, [FromQuery] int year)
    {
        var dto = await performanceService.GetMetricsAsync(technicianId, month, year);
        if (dto == null)
            return NotFound(ApiResponse<object>.Fail($"No performance metrics computed for {technicianId} in {month}/{year}.", 404));
        return Ok(ApiResponse<object>.Ok(dto));
    }

    [HttpGet("departments/{departmentId}/summary")]
    [Authorize(Policy = "Permission:operations.read.dept")]
    public async Task<IActionResult> GetDepartmentSummary(string departmentId, [FromQuery] int month, [FromQuery] int year)
    {
        var dto = await performanceService.GetDepartmentSummaryAsync(departmentId, month, year);
        return Ok(ApiResponse<object>.Ok(dto));
    }

    [HttpPost("technicians/{technicianId}/compute")]
    [Authorize(Policy = "Permission:operations.approve")]
    public async Task<IActionResult> ComputeTechnicianMetrics(string technicianId, [FromQuery] int month, [FromQuery] int year)
    {
        await performanceService.ComputeMetricsAsync(technicianId, month, year);
        var dto = await performanceService.GetMetricsAsync(technicianId, month, year);
        if (dto == null)
            return Ok(ApiResponse<object>.Fail($"Computed but no metrics row found for {technicianId}, {month}/{year} -- report this.", 500));
        return Ok(ApiResponse<object>.Ok(dto, $"Metrics computed for {technicianId}, {month}/{year}."));
    }

    [HttpPost("departments/{departmentId}/compute")]
    [Authorize(Policy = "Permission:operations.approve")]
    public async Task<IActionResult> ComputeDepartmentMetrics(string departmentId, [FromQuery] int month, [FromQuery] int year)
    {
        await performanceService.ComputeDepartmentMetricsAsync(departmentId, month, year);
        var dto = await performanceService.GetDepartmentSummaryAsync(departmentId, month, year);
        return Ok(ApiResponse<object>.Ok(dto, $"Metrics computed for department {departmentId}, {month}/{year}."));
    }
}
