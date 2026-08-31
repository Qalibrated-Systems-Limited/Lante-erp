using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OperationsService.Core.DTOs.Common;
using OperationsService.Core.DTOs.Handovers;
using OperationsService.Core.Interfaces.Services;

namespace OperationsService.Api.Controllers;

/// <summary>O9 — project handovers (8-step, four mandatory signatures, permanent once completed).</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/handovers")]
[Authorize]
public class ProjectHandoversController(IProjectHandoverService service) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "Permission:projects.read.own")]
    public async Task<ActionResult<ApiResponse<HandoverReadDto>>> GetById(Guid id)
    {
        var h = await service.GetByIdAsync(id.ToString());
        if (h is null) return NotFound(new ApiResponse<HandoverReadDto> { Success = false, Message = "Handover not found." });
        return Ok(new ApiResponse<HandoverReadDto> { Success = true, Data = h });
    }

    [HttpGet("by-project/{projectId:guid}")]
    [Authorize(Policy = "Permission:projects.read.own")]
    public async Task<ActionResult<ApiResponse<IEnumerable<HandoverReadDto>>>> GetByProject(Guid projectId)
    {
        var items = await service.GetByProjectAsync(projectId.ToString());
        return Ok(new ApiResponse<IEnumerable<HandoverReadDto>> { Success = true, Data = items });
    }

    [HttpPost]
    [Authorize(Policy = "Permission:projects.write")]
    public async Task<ActionResult<ApiResponse<HandoverReadDto>>> Create([FromBody] CreateHandoverDto dto)
    {
        var h = await service.CreateAsync(dto, UserId);
        return CreatedAtAction(nameof(GetById), new { id = h.Id }, new ApiResponse<HandoverReadDto> { Success = true, Data = h });
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "Permission:projects.write")]
    public async Task<ActionResult<ApiResponse<HandoverReadDto>>> Update(Guid id, [FromBody] UpdateHandoverDto dto)
    {
        var h = await service.UpdateAsync(id.ToString(), dto, UserId);
        return Ok(new ApiResponse<HandoverReadDto> { Success = true, Data = h });
    }

    [HttpPost("{id:guid}/signatures")]
    [Authorize(Policy = "Permission:projects.write")]
    public async Task<ActionResult<ApiResponse<HandoverReadDto>>> AddSignature(Guid id, [FromBody] AddHandoverSignatureDto dto)
    {
        var h = await service.AddSignatureAsync(id.ToString(), dto, UserId);
        return Ok(new ApiResponse<HandoverReadDto> { Success = true, Data = h });
    }

    [HttpPost("{id:guid}/complete")]
    [Authorize(Policy = "Permission:projects.approve")]
    public async Task<ActionResult<ApiResponse<HandoverReadDto>>> Complete(Guid id)
    {
        var h = await service.CompleteAsync(id.ToString(), UserId);
        return Ok(new ApiResponse<HandoverReadDto> { Success = true, Data = h });
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "Permission:projects.write")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(Guid id)
    {
        await service.DeleteAsync(id.ToString(), UserId);
        return Ok(new ApiResponse<object> { Success = true, Message = "Handover deleted." });
    }
}
