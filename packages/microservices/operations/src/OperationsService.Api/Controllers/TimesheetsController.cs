using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OperationsService.Core.DTOs.Common;
using OperationsService.Core.DTOs.Timesheets;
using OperationsService.Core.Interfaces.Services;

namespace OperationsService.Api.Controllers;

/// <summary>O4 — weekly timesheets: entry capture, overtime pre-approval, line-manager approval.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/timesheets")]
[Authorize]
public class TimesheetsController(ITimesheetService service) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
    private string UserName => User.FindFirstValue(ClaimTypes.Name) ?? User.FindFirstValue("name") ?? string.Empty;
    private string DepartmentId => User.FindFirstValue("department_id") ?? string.Empty;

    [HttpGet]
    [Authorize(Policy = "Permission:operations.read.own")]
    public async Task<ActionResult<ApiResponse<PaginatedResult<TimesheetReadDto>>>> GetAll([FromQuery] TimesheetFilterParameters filters)
    {
        // operations.read.own without a broader scope sees only the caller's own timesheets.
        if (!User.HasClaim("permission", "operations.read.all") && !User.HasClaim("permission", "operations.approve"))
            filters.EmployeeId = UserId;
        var result = await service.GetAllAsync(filters);
        return Ok(new ApiResponse<PaginatedResult<TimesheetReadDto>> { Success = true, Data = result });
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "Permission:operations.read.own")]
    public async Task<ActionResult<ApiResponse<TimesheetReadDto>>> GetById(Guid id)
    {
        var ts = await service.GetByIdAsync(id.ToString());
        if (ts is null) return NotFound(new ApiResponse<TimesheetReadDto> { Success = false, Message = "Timesheet not found." });
        return Ok(new ApiResponse<TimesheetReadDto> { Success = true, Data = ts });
    }

    [HttpPost]
    [Authorize(Policy = "Permission:operations.read.own")]
    public async Task<ActionResult<ApiResponse<TimesheetReadDto>>> Create([FromBody] CreateTimesheetDto dto)
    {
        var ts = await service.CreateAsync(dto, UserId, UserName, DepartmentId);
        return CreatedAtAction(nameof(GetById), new { id = ts.Id }, new ApiResponse<TimesheetReadDto> { Success = true, Data = ts });
    }

    [HttpPost("{id:guid}/entries")]
    [Authorize(Policy = "Permission:operations.read.own")]
    public async Task<ActionResult<ApiResponse<TimesheetEntryReadDto>>> AddEntry(Guid id, [FromBody] CreateTimesheetEntryDto dto)
    {
        var entry = await service.AddEntryAsync(id.ToString(), dto, UserId);
        return Ok(new ApiResponse<TimesheetEntryReadDto> { Success = true, Data = entry });
    }

    [HttpPut("entries/{entryId:guid}")]
    [Authorize(Policy = "Permission:operations.read.own")]
    public async Task<ActionResult<ApiResponse<TimesheetEntryReadDto>>> UpdateEntry(Guid entryId, [FromBody] UpdateTimesheetEntryDto dto)
    {
        var entry = await service.UpdateEntryAsync(entryId.ToString(), dto, UserId);
        return Ok(new ApiResponse<TimesheetEntryReadDto> { Success = true, Data = entry });
    }

    [HttpDelete("entries/{entryId:guid}")]
    [Authorize(Policy = "Permission:operations.read.own")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteEntry(Guid entryId)
    {
        await service.DeleteEntryAsync(entryId.ToString(), UserId);
        return Ok(new ApiResponse<object> { Success = true, Message = "Entry deleted." });
    }

    [HttpPost("entries/{entryId:guid}/overtime-request")]
    [Authorize(Policy = "Permission:operations.read.own")]
    public async Task<ActionResult<ApiResponse<TimesheetEntryReadDto>>> RequestOvertime(Guid entryId, [FromBody] RequestOvertimeDto dto)
    {
        var entry = await service.RequestOvertimeAsync(entryId.ToString(), dto, UserId);
        return Ok(new ApiResponse<TimesheetEntryReadDto> { Success = true, Data = entry });
    }

    [HttpPost("{id:guid}/submit")]
    [Authorize(Policy = "Permission:operations.read.own")]
    public async Task<ActionResult<ApiResponse<TimesheetReadDto>>> Submit(Guid id)
    {
        var ts = await service.SubmitAsync(id.ToString(), UserId);
        return Ok(new ApiResponse<TimesheetReadDto> { Success = true, Data = ts });
    }

    // Weekly line-manager approval → posts labour (Finance) + hours (HR payroll).
    [HttpPost("{id:guid}/review")]
    [Authorize(Policy = "Permission:operations.approve")]
    public async Task<ActionResult<ApiResponse<TimesheetReadDto>>> Review(Guid id, [FromBody] ReviewTimesheetDto dto)
    {
        var ts = await service.ReviewAsync(id.ToString(), dto, UserId, UserName);
        return Ok(new ApiResponse<TimesheetReadDto> { Success = true, Data = ts });
    }
}
