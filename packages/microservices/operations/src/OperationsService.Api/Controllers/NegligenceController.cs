using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OperationsService.Core.DTOs.Common;
using OperationsService.Core.DTOs.Negligence;
using OperationsService.Core.Interfaces.Services;

namespace OperationsService.Api.Controllers;

/// <summary>O9 — negligence incidents (24h log, 5-day response, repeat-offense final warning).</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/negligence-incidents")]
[Authorize]
public class NegligenceController(INegligenceService service) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
    private string UserName => User.FindFirstValue(ClaimTypes.Name) ?? User.FindFirstValue("name") ?? string.Empty;

    [HttpGet]
    [Authorize(Policy = "Permission:operations.read.dept")]
    public async Task<ActionResult<ApiResponse<PaginatedResult<NegligenceIncidentReadDto>>>> GetAll(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? employeeId = null, [FromQuery] string? status = null)
    {
        var result = await service.GetAllAsync(page, pageSize, employeeId, status);
        return Ok(new ApiResponse<PaginatedResult<NegligenceIncidentReadDto>> { Success = true, Data = result });
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "Permission:operations.read.dept")]
    public async Task<ActionResult<ApiResponse<NegligenceIncidentReadDto>>> GetById(Guid id)
    {
        var i = await service.GetByIdAsync(id.ToString());
        if (i is null) return NotFound(new ApiResponse<NegligenceIncidentReadDto> { Success = false, Message = "Incident not found." });
        return Ok(new ApiResponse<NegligenceIncidentReadDto> { Success = true, Data = i });
    }

    [HttpPost]
    [Authorize(Policy = "Permission:operations.write")]
    public async Task<ActionResult<ApiResponse<NegligenceIncidentReadDto>>> Report([FromBody] ReportNegligenceDto dto)
    {
        var i = await service.ReportAsync(dto, UserId);
        return CreatedAtAction(nameof(GetById), new { id = i.Id }, new ApiResponse<NegligenceIncidentReadDto> { Success = true, Data = i });
    }

    [HttpPost("{id:guid}/responses")]
    [Authorize(Policy = "Permission:operations.approve")]
    public async Task<ActionResult<ApiResponse<NegligenceIncidentReadDto>>> Respond(Guid id, [FromBody] RespondNegligenceDto dto)
    {
        var i = await service.RespondAsync(id.ToString(), dto, UserId, UserName);
        return Ok(new ApiResponse<NegligenceIncidentReadDto> { Success = true, Data = i });
    }
}
