using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OperationsService.Core.DTOs.Common;
using OperationsService.Core.DTOs.Variations;
using OperationsService.Core.Interfaces.Services;

namespace OperationsService.Api.Controllers;

/// <summary>O7 — variation orders (Draft → MD approval → client approval → apply) + the read-only
/// subcontractor site-start compliance check (ASR + RAMS) that operations enforces via seam.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/variation-orders")]
[Authorize]
public class VariationOrdersController(
    IVariationOrderService service,
    ISubcontractorComplianceGateway subcontractors) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "Permission:projects.read.own")]
    public async Task<ActionResult<ApiResponse<VariationOrderReadDto>>> GetById(Guid id)
    {
        var vo = await service.GetByIdAsync(id.ToString());
        if (vo is null) return NotFound(new ApiResponse<VariationOrderReadDto> { Success = false, Message = "Variation order not found." });
        return Ok(new ApiResponse<VariationOrderReadDto> { Success = true, Data = vo });
    }

    [HttpGet("by-project/{projectId:guid}")]
    [Authorize(Policy = "Permission:projects.read.own")]
    public async Task<ActionResult<ApiResponse<IEnumerable<VariationOrderReadDto>>>> GetByProject(Guid projectId)
    {
        var items = await service.GetByProjectAsync(projectId.ToString());
        return Ok(new ApiResponse<IEnumerable<VariationOrderReadDto>> { Success = true, Data = items });
    }

    [HttpPost]
    [Authorize(Policy = "Permission:projects.write")]
    public async Task<ActionResult<ApiResponse<VariationOrderReadDto>>> Create([FromBody] CreateVariationOrderDto dto)
    {
        var vo = await service.CreateAsync(dto, UserId);
        return CreatedAtAction(nameof(GetById), new { id = vo.Id }, new ApiResponse<VariationOrderReadDto> { Success = true, Data = vo });
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "Permission:projects.write")]
    public async Task<ActionResult<ApiResponse<VariationOrderReadDto>>> Update(Guid id, [FromBody] UpdateVariationOrderDto dto)
    {
        var vo = await service.UpdateAsync(id.ToString(), dto, UserId);
        return Ok(new ApiResponse<VariationOrderReadDto> { Success = true, Data = vo });
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "Permission:projects.write")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(Guid id)
    {
        await service.DeleteAsync(id.ToString(), UserId);
        return Ok(new ApiResponse<object> { Success = true, Message = "Variation order deleted." });
    }

    [HttpPost("{id:guid}/submit")]
    [Authorize(Policy = "Permission:projects.write")]
    public async Task<ActionResult<ApiResponse<VariationOrderReadDto>>> Submit(Guid id)
    {
        var vo = await service.SubmitForApprovalAsync(id.ToString(), UserId);
        return Ok(new ApiResponse<VariationOrderReadDto> { Success = true, Data = vo });
    }

    [HttpPost("{id:guid}/approve/md")]
    [Authorize(Policy = "Permission:projects.approve")]
    public async Task<ActionResult<ApiResponse<VariationOrderReadDto>>> MdReview(Guid id, [FromBody] ReviewVariationOrderDto dto)
    {
        var vo = await service.MdReviewAsync(id.ToString(), dto, UserId);
        return Ok(new ApiResponse<VariationOrderReadDto> { Success = true, Data = vo });
    }

    // Client approval applies the change (contract value + budget line + invoice).
    [HttpPost("{id:guid}/approve/client")]
    [Authorize(Policy = "Permission:projects.write")]
    public async Task<ActionResult<ApiResponse<VariationOrderReadDto>>> ClientApprove(Guid id, [FromBody] ClientApproveVariationOrderDto dto)
    {
        var vo = await service.ClientApproveAsync(id.ToString(), dto, UserId);
        return Ok(new ApiResponse<VariationOrderReadDto> { Success = true, Data = vo });
    }

    // O7 (DEC-2) — read-only ASR + RAMS gate check against subcontracts-service (via seam).
    [HttpGet("/api/v{version:apiVersion}/subcontractor-compliance/{subcontractorId}")]
    [Authorize(Policy = "Permission:operations.read.own")]
    public async Task<ActionResult<ApiResponse<SubcontractorComplianceResult>>> CheckSubcontractorCompliance(string subcontractorId)
    {
        var result = await subcontractors.CheckSiteStartComplianceAsync(subcontractorId);
        return Ok(new ApiResponse<SubcontractorComplianceResult> { Success = true, Data = result });
    }
}
