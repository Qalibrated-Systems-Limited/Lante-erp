using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OperationsService.Core.DTOs.Common;
using OperationsService.Core.DTOs.ServiceReports;
using OperationsService.Core.Interfaces.Services;
using System.Security.Claims;

namespace OperationsService.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/service-reports")]
[Authorize]
public class ServiceReportsController(IServiceReportService serviceReportService) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
    private string UserName => User.FindFirstValue(ClaimTypes.Name) ?? User.FindFirstValue("name") ?? string.Empty;

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<ServiceReportReadDto>>> GetById(Guid id)
    {
        var report = await serviceReportService.GetByIdAsync(id.ToString());
        if (report is null) return NotFound(new ApiResponse<ServiceReportReadDto> { Success = false, Message = "Report not found." });
        return Ok(new ApiResponse<ServiceReportReadDto> { Success = true, Data = report });
    }

    [HttpGet("by-assignment/{assignmentId:guid}")]
    public async Task<ActionResult<ApiResponse<ServiceReportReadDto>>> GetByAssignment(Guid assignmentId)
    {
        var report = await serviceReportService.GetByAssignmentIdAsync(assignmentId.ToString());
        if (report is null) return NotFound(new ApiResponse<ServiceReportReadDto> { Success = false, Message = "Report not found." });
        return Ok(new ApiResponse<ServiceReportReadDto> { Success = true, Data = report });
    }

    // O5-FSR — per-serial equipment service history across all field service reports.
    [HttpGet("equipment/history")]
    public async Task<ActionResult<ApiResponse<IEnumerable<FsrEquipmentDto>>>> GetEquipmentHistory([FromQuery] string serial)
    {
        var history = await serviceReportService.GetEquipmentHistoryAsync(serial);
        return Ok(new ApiResponse<IEnumerable<FsrEquipmentDto>> { Success = true, Data = history });
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<ServiceReportReadDto>>> Create([FromBody] CreateServiceReportDto dto)
    {
        var report = await serviceReportService.CreateAsync(dto, UserId, UserName);
        return Ok(new ApiResponse<ServiceReportReadDto> { Success = true, Data = report });
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<ServiceReportReadDto>>> Update(Guid id, [FromBody] UpdateServiceReportDto dto)
    {
        var report = await serviceReportService.UpdateAsync(id.ToString(), dto, UserId);
        return Ok(new ApiResponse<ServiceReportReadDto> { Success = true, Data = report });
    }

    [HttpPost("{id:guid}/sign")]
    public async Task<ActionResult<ApiResponse<ServiceReportReadDto>>> Sign(Guid id, [FromBody] SignServiceReportDto dto)
    {
        var report = await serviceReportService.SignAsync(id.ToString(), dto, UserId);
        return Ok(new ApiResponse<ServiceReportReadDto> { Success = true, Data = report });
    }

    [HttpPost("{id:guid}/review")]
    [Authorize(Policy = "Permission:operations.approve")]
    public async Task<ActionResult<ApiResponse<ServiceReportReadDto>>> Review(Guid id, [FromBody] ReviewServiceReportDto dto)
    {
        var report = await serviceReportService.ReviewAsync(id.ToString(), dto, UserId, UserName);
        return Ok(new ApiResponse<ServiceReportReadDto> { Success = true, Data = report });
    }
}
