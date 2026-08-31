using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SubcontractsService.Core.DTOs.Common;
using SubcontractsService.Core.DTOs.Scorecards;
using SubcontractsService.Core.Entities;
using SubcontractsService.Core.Interfaces.Services;

namespace SubcontractsService.Api.Controllers;

// SUB-006: PM-completed performance scorecard; auto-syncs the ASR watch list.
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/subcon-scorecards")]
public class SubconScorecardsController(
    ISubcontractsCrudService<SubconScorecard> scorecards,
    IScorecardWorkflowService workflow) : ControllerBase
{
    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    [HttpGet]
    [Authorize(Policy = "subcontracts.read")]
    public async Task<ActionResult<ApiResponse<PaginatedResult<SubconScorecardReadDto>>>> GetAll(
        [FromQuery] string? awardId, [FromQuery] PaginationParameters parameters)
    {
        PaginatedResult<SubconScorecardReadDto> result;
        if (string.IsNullOrWhiteSpace(awardId))
        {
            var paged = await scorecards.GetPagedAsync(parameters);
            result = new PaginatedResult<SubconScorecardReadDto>
            {
                Items = paged.Items.Select(ToReadDto).ToList(),
                TotalCount = paged.TotalCount,
                Page = paged.Page,
                PageSize = paged.PageSize,
            };
        }
        else
        {
            var filtered = await scorecards.FindAsync(s => s.AwardId == awardId);
            result = new PaginatedResult<SubconScorecardReadDto>
            {
                Items = filtered.Skip((parameters.Page - 1) * parameters.PageSize).Take(parameters.PageSize).Select(ToReadDto).ToList(),
                TotalCount = filtered.Count,
                Page = parameters.Page,
                PageSize = parameters.PageSize,
            };
        }
        return Ok(ApiResponse<PaginatedResult<SubconScorecardReadDto>>.Ok(result));
    }

    [HttpPost]
    [Authorize(Policy = "subcontracts.write")]
    public async Task<ActionResult<ApiResponse<SubconScorecardReadDto>>> Create([FromBody] CreateSubconScorecardDto dto)
    {
        var scorecard = new SubconScorecard
        {
            AwardId = dto.AwardId,
            ProjectManagerUserId = CurrentUserId,
            ProjectManagerName = dto.ProjectManagerName,
            Score = dto.Score,
            CompletedOn = dto.CompletedOn,
            Notes = dto.Notes,
        };
        await workflow.RecordAsync(scorecard, CurrentUserId);
        return Ok(ApiResponse<SubconScorecardReadDto>.Ok(ToReadDto(scorecard), "Scorecard recorded — ASR performance updated."));
    }

    [HttpGet("{id}")]
    [Authorize(Policy = "subcontracts.read")]
    public async Task<ActionResult<ApiResponse<SubconScorecardReadDto>>> GetById(string id)
    {
        var scorecard = await scorecards.GetByIdAsync(id);
        if (scorecard == null) return NotFound(ApiResponse<SubconScorecardReadDto>.Fail("Scorecard not found.", 404));
        return Ok(ApiResponse<SubconScorecardReadDto>.Ok(ToReadDto(scorecard)));
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "subcontracts.write")]
    public async Task<ActionResult<ApiResponse<SubconScorecardReadDto>>> Update(string id, [FromBody] UpdateSubconScorecardDto dto)
    {
        var scorecard = await scorecards.GetByIdAsync(id);
        if (scorecard == null) return NotFound(ApiResponse<SubconScorecardReadDto>.Fail("Scorecard not found.", 404));

        scorecard.ProjectManagerName = dto.ProjectManagerName;
        scorecard.Score = dto.Score;
        scorecard.CompletedOn = dto.CompletedOn;
        scorecard.Notes = dto.Notes;
        await scorecards.UpdateAsync(scorecard);

        return Ok(ApiResponse<SubconScorecardReadDto>.Ok(ToReadDto(scorecard), "Scorecard updated."));
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "subcontracts.delete")]
    public async Task<ActionResult<ApiResponse>> Delete(string id)
    {
        var deleted = await scorecards.DeleteAsync(id);
        if (!deleted) return NotFound(new ApiResponse { Success = false, Message = "Scorecard not found.", StatusCode = 404 });
        return Ok(new ApiResponse { Success = true, Message = "Scorecard deleted successfully.", StatusCode = 200 });
    }

    private static SubconScorecardReadDto ToReadDto(SubconScorecard s) => new()
    {
        Id = s.Id,
        AwardId = s.AwardId,
        ProjectManagerUserId = s.ProjectManagerUserId,
        ProjectManagerName = s.ProjectManagerName,
        Score = s.Score,
        CompletedOn = s.CompletedOn,
        Notes = s.Notes,
    };
}
