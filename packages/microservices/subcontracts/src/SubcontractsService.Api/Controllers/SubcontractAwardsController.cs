using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SubcontractsService.Core.DTOs.Awards;
using SubcontractsService.Core.DTOs.Common;
using SubcontractsService.Core.Entities;
using SubcontractsService.Core.Interfaces.Services;

namespace SubcontractsService.Api.Controllers;

// SUB-004: awards linked to project/value/status. SUB-005: mobilization is gated on HSE RAMS approval.
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/subcontract-awards")]
public class SubcontractAwardsController(
    ISubcontractsCrudService<SubcontractAward> awards,
    IAwardWorkflowService workflow) : ControllerBase
{
    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
    private string? CurrentTenantSchema => User.FindFirstValue("schema");

    [HttpGet]
    [Authorize(Policy = "subcontracts.read")]
    public async Task<ActionResult<ApiResponse<PaginatedResult<SubcontractAwardReadDto>>>> GetAll(
        [FromQuery] string? subcontractorId, [FromQuery] PaginationParameters parameters)
    {
        PaginatedResult<SubcontractAwardReadDto> result;
        if (string.IsNullOrWhiteSpace(subcontractorId))
        {
            var paged = await awards.GetPagedAsync(parameters);
            result = new PaginatedResult<SubcontractAwardReadDto>
            {
                Items = paged.Items.Select(ToReadDto).ToList(),
                TotalCount = paged.TotalCount,
                Page = paged.Page,
                PageSize = paged.PageSize,
            };
        }
        else
        {
            var filtered = await awards.FindAsync(a => a.SubcontractorId == subcontractorId);
            result = new PaginatedResult<SubcontractAwardReadDto>
            {
                Items = filtered.Skip((parameters.Page - 1) * parameters.PageSize).Take(parameters.PageSize).Select(ToReadDto).ToList(),
                TotalCount = filtered.Count,
                Page = parameters.Page,
                PageSize = parameters.PageSize,
            };
        }
        return Ok(ApiResponse<PaginatedResult<SubcontractAwardReadDto>>.Ok(result));
    }

    [HttpGet("{id}")]
    [Authorize(Policy = "subcontracts.read")]
    public async Task<ActionResult<ApiResponse<SubcontractAwardReadDto>>> GetById(string id)
    {
        var award = await awards.GetByIdAsync(id);
        if (award == null) return NotFound(ApiResponse<SubcontractAwardReadDto>.Fail("Award not found.", 404));
        return Ok(ApiResponse<SubcontractAwardReadDto>.Ok(ToReadDto(award)));
    }

    [HttpPost]
    [Authorize(Policy = "subcontracts.write")]
    public async Task<ActionResult<ApiResponse<SubcontractAwardReadDto>>> Create([FromBody] CreateSubcontractAwardDto dto)
    {
        var award = new SubcontractAward
        {
            SubcontractorId = dto.SubcontractorId,
            SubcontractorName = dto.SubcontractorName,
            ProjectId = dto.ProjectId,
            ProjectName = dto.ProjectName,
            Value = dto.Value,
            SignedAgreementUrl = dto.SignedAgreementUrl,
        };
        await awards.CreateAsync(award);
        return CreatedAtAction(nameof(GetById), new { id = award.Id, version = "1" },
            ApiResponse<SubcontractAwardReadDto>.Ok(ToReadDto(award), "Award created — pending approval."));
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "subcontracts.write")]
    public async Task<ActionResult<ApiResponse<SubcontractAwardReadDto>>> Update(string id, [FromBody] UpdateSubcontractAwardDto dto)
    {
        var award = await awards.GetByIdAsync(id);
        if (award == null) return NotFound(ApiResponse<SubcontractAwardReadDto>.Fail("Award not found.", 404));

        award.SubcontractorId = dto.SubcontractorId;
        award.SubcontractorName = dto.SubcontractorName;
        award.ProjectId = dto.ProjectId;
        award.ProjectName = dto.ProjectName;
        award.Value = dto.Value;
        award.SignedAgreementUrl = dto.SignedAgreementUrl;
        await awards.UpdateAsync(award);

        return Ok(ApiResponse<SubcontractAwardReadDto>.Ok(ToReadDto(award), "Award updated."));
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "subcontracts.delete")]
    public async Task<ActionResult<ApiResponse>> Delete(string id)
    {
        var deleted = await awards.DeleteAsync(id);
        if (!deleted) return NotFound(new ApiResponse { Success = false, Message = "Award not found.", StatusCode = 404 });
        return Ok(new ApiResponse { Success = true, Message = "Award deleted successfully.", StatusCode = 200 });
    }

    [HttpPatch("{id}/status")]
    [Authorize(Policy = "subcontracts.approve")]
    public async Task<ActionResult<ApiResponse<SubcontractAwardReadDto>>> UpdateStatus(string id, [FromBody] UpdateAwardStatusDto dto)
    {
        var award = await awards.GetByIdAsync(id);
        if (award == null) return NotFound(ApiResponse<SubcontractAwardReadDto>.Fail("Award not found.", 404));

        award.Status = dto.Status;
        await awards.UpdateAsync(award);
        return Ok(ApiResponse<SubcontractAwardReadDto>.Ok(ToReadDto(award), "Award status updated."));
    }

    [HttpPatch("{id}/activate-mobilization")]
    [Authorize(Policy = "subcontracts.approve")]
    public async Task<ActionResult<ApiResponse<SubcontractAwardReadDto>>> ActivateMobilization(string id)
    {
        var schema = CurrentTenantSchema ?? string.Empty;
        var award = await workflow.ActivateMobilizationAsync(schema, id, CurrentUserId);
        return Ok(ApiResponse<SubcontractAwardReadDto>.Ok(ToReadDto(award), "Mobilization activated — RAMS approval confirmed."));
    }

    private static SubcontractAwardReadDto ToReadDto(SubcontractAward a) => new()
    {
        Id = a.Id,
        SubcontractorId = a.SubcontractorId,
        SubcontractorName = a.SubcontractorName,
        ProjectId = a.ProjectId,
        ProjectName = a.ProjectName,
        Value = a.Value,
        Status = a.Status.ToString(),
        SignedAgreementUrl = a.SignedAgreementUrl,
        RamsApproved = a.RamsApproved,
        MobilizationActivatedAt = a.MobilizationActivatedAt,
    };
}
