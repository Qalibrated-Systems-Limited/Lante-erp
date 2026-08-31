using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SubcontractsService.Core.DTOs.Common;
using SubcontractsService.Core.DTOs.Prequalifications;
using SubcontractsService.Core.Entities;
using SubcontractsService.Core.Enums;
using SubcontractsService.Core.Interfaces.Services;

namespace SubcontractsService.Api.Controllers;

// SUB-002: PQQ sent/completed/scored; approval by Head of Projects auto-updates the ASR.
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/prequalifications")]
public class PrequalificationsController(
    ISubcontractsCrudService<Prequalification> pqqs,
    IPrequalificationWorkflowService workflow) : ControllerBase
{
    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
    private string CurrentUserName => User.FindFirstValue("name") ?? User.FindFirstValue(ClaimTypes.Name) ?? CurrentUserId;

    [HttpGet]
    [Authorize(Policy = "subcontracts.read")]
    public async Task<ActionResult<ApiResponse<PaginatedResult<PrequalificationReadDto>>>> GetAll(
        [FromQuery] string? subcontractorId, [FromQuery] PaginationParameters parameters)
    {
        PaginatedResult<PrequalificationReadDto> result;
        if (string.IsNullOrWhiteSpace(subcontractorId))
        {
            var paged = await pqqs.GetPagedAsync(parameters);
            result = new PaginatedResult<PrequalificationReadDto>
            {
                Items = paged.Items.Select(ToReadDto).ToList(),
                TotalCount = paged.TotalCount,
                Page = paged.Page,
                PageSize = paged.PageSize,
            };
        }
        else
        {
            var filtered = await pqqs.FindAsync(p => p.SubcontractorId == subcontractorId);
            result = new PaginatedResult<PrequalificationReadDto>
            {
                Items = filtered.Skip((parameters.Page - 1) * parameters.PageSize).Take(parameters.PageSize).Select(ToReadDto).ToList(),
                TotalCount = filtered.Count,
                Page = parameters.Page,
                PageSize = parameters.PageSize,
            };
        }
        return Ok(ApiResponse<PaginatedResult<PrequalificationReadDto>>.Ok(result));
    }

    [HttpGet("{id}")]
    [Authorize(Policy = "subcontracts.read")]
    public async Task<ActionResult<ApiResponse<PrequalificationReadDto>>> GetById(string id)
    {
        var pqq = await pqqs.GetByIdAsync(id);
        if (pqq == null) return NotFound(ApiResponse<PrequalificationReadDto>.Fail("Prequalification not found.", 404));
        return Ok(ApiResponse<PrequalificationReadDto>.Ok(ToReadDto(pqq)));
    }

    [HttpPost]
    [Authorize(Policy = "subcontracts.write")]
    public async Task<ActionResult<ApiResponse<PrequalificationReadDto>>> Create([FromBody] CreatePrequalificationDto dto)
    {
        var pqq = new Prequalification
        {
            SubcontractorId = dto.SubcontractorId,
            DocumentUrl = dto.DocumentUrl,
            SubmittedOn = dto.SubmittedOn,
            Status = dto.DocumentUrl != null ? PrequalificationStatus.Completed : PrequalificationStatus.Sent,
        };
        await pqqs.CreateAsync(pqq);
        return CreatedAtAction(nameof(GetById), new { id = pqq.Id, version = "1" },
            ApiResponse<PrequalificationReadDto>.Ok(ToReadDto(pqq), "Prequalification recorded."));
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "subcontracts.write")]
    public async Task<ActionResult<ApiResponse<PrequalificationReadDto>>> Update(string id, [FromBody] UpdatePrequalificationDto dto)
    {
        var pqq = await pqqs.GetByIdAsync(id);
        if (pqq == null) return NotFound(ApiResponse<PrequalificationReadDto>.Fail("Prequalification not found.", 404));

        pqq.DocumentUrl = dto.DocumentUrl;
        pqq.SubmittedOn = dto.SubmittedOn;
        if (pqq.Status is PrequalificationStatus.Sent or PrequalificationStatus.Completed)
            pqq.Status = dto.DocumentUrl != null ? PrequalificationStatus.Completed : PrequalificationStatus.Sent;
        await pqqs.UpdateAsync(pqq);

        return Ok(ApiResponse<PrequalificationReadDto>.Ok(ToReadDto(pqq), "Prequalification updated."));
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "subcontracts.delete")]
    public async Task<ActionResult<ApiResponse>> Delete(string id)
    {
        var deleted = await pqqs.DeleteAsync(id);
        if (!deleted) return NotFound(new ApiResponse { Success = false, Message = "Prequalification not found.", StatusCode = 404 });
        return Ok(new ApiResponse { Success = true, Message = "Prequalification deleted successfully.", StatusCode = 200 });
    }

    [HttpPatch("{id}/approve")]
    [Authorize(Policy = "subcontracts.approve")]
    public async Task<ActionResult<ApiResponse<PrequalificationReadDto>>> Approve(string id, [FromBody] ApprovePrequalificationDto dto)
    {
        var pqq = await workflow.ApproveAsync(id, dto.Score, CurrentUserId, CurrentUserName);
        return Ok(ApiResponse<PrequalificationReadDto>.Ok(ToReadDto(pqq), "Prequalification approved — ASR updated."));
    }

    [HttpPatch("{id}/reject")]
    [Authorize(Policy = "subcontracts.approve")]
    public async Task<ActionResult<ApiResponse<PrequalificationReadDto>>> Reject(string id)
    {
        var pqq = await workflow.RejectAsync(id, CurrentUserId, CurrentUserName);
        return Ok(ApiResponse<PrequalificationReadDto>.Ok(ToReadDto(pqq), "Prequalification rejected."));
    }

    private static PrequalificationReadDto ToReadDto(Prequalification p) => new()
    {
        Id = p.Id,
        SubcontractorId = p.SubcontractorId,
        Score = p.Score,
        DocumentUrl = p.DocumentUrl,
        Status = p.Status.ToString(),
        SubmittedOn = p.SubmittedOn,
        ApprovedByUserId = p.ApprovedByUserId,
        ApprovedByName = p.ApprovedByName,
        ApprovedOn = p.ApprovedOn,
    };
}
