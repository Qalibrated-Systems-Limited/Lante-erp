using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OperationsService.Core.DTOs.Calibration;
using OperationsService.Core.DTOs.Common;
using OperationsService.Core.Interfaces.Services;

namespace OperationsService.Api.Controllers;

/// <summary>O6 — reference-standard register (traceability + expiry) for calibration.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/reference-standards")]
[Authorize]
public class ReferenceStandardsController(IReferenceStandardService service) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    [HttpGet]
    [Authorize(Policy = "Permission:operations.read.own")]
    public async Task<ActionResult<ApiResponse<PaginatedResult<ReferenceStandardReadDto>>>> GetAll(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 50, [FromQuery] bool activeOnly = false)
    {
        var result = await service.GetAllAsync(page, pageSize, activeOnly);
        return Ok(new ApiResponse<PaginatedResult<ReferenceStandardReadDto>> { Success = true, Data = result });
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "Permission:operations.read.own")]
    public async Task<ActionResult<ApiResponse<ReferenceStandardReadDto>>> GetById(Guid id)
    {
        var s = await service.GetByIdAsync(id.ToString());
        if (s is null) return NotFound(new ApiResponse<ReferenceStandardReadDto> { Success = false, Message = "Reference standard not found." });
        return Ok(new ApiResponse<ReferenceStandardReadDto> { Success = true, Data = s });
    }

    [HttpPost]
    [Authorize(Policy = "Permission:operations.write")]
    public async Task<ActionResult<ApiResponse<ReferenceStandardReadDto>>> Create([FromBody] CreateReferenceStandardDto dto)
    {
        var s = await service.CreateAsync(dto, UserId);
        return CreatedAtAction(nameof(GetById), new { id = s.Id }, new ApiResponse<ReferenceStandardReadDto> { Success = true, Data = s });
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "Permission:operations.write")]
    public async Task<ActionResult<ApiResponse<ReferenceStandardReadDto>>> Update(Guid id, [FromBody] UpdateReferenceStandardDto dto)
    {
        var s = await service.UpdateAsync(id.ToString(), dto, UserId);
        return Ok(new ApiResponse<ReferenceStandardReadDto> { Success = true, Data = s });
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "Permission:operations.delete")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(Guid id)
    {
        await service.DeleteAsync(id.ToString(), UserId);
        return Ok(new ApiResponse<object> { Success = true, Message = "Reference standard retired." });
    }
}
