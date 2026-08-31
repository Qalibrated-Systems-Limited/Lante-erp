using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HSEService.Core.DTOs.Common;
using HSEService.Core.DTOs.Ppe;
using HSEService.Core.Entities;
using HSEService.Core.Interfaces.Repositories;
using HSEService.Core.Interfaces.Services;

namespace HSEService.Api.Controllers;

// HSE-003: PPE issue register — item issued, to whom, date, condition, return/replacement tracking.
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/hse-ppe-issues")]
public class PpeIssuesController(IHseCrudService<PpeIssue> ppeIssues, IPpeIssueRepository ppeIssueRepository) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "hse.read")]
    public async Task<ActionResult<ApiResponse<PaginatedResult<PpeIssueReadDto>>>> GetAll([FromQuery] PpeIssueFilterParameters parameters)
    {
        var paged = await ppeIssueRepository.GetPagedAsync(parameters);
        var dtoItems = paged.Items.Select(ToReadDto).ToList();
        var result = new PaginatedResult<PpeIssueReadDto>
        {
            Items = dtoItems,
            TotalCount = paged.TotalCount,
            Page = paged.Page,
            PageSize = paged.PageSize
        };
        return Ok(ApiResponse<PaginatedResult<PpeIssueReadDto>>.Ok(result));
    }

    [HttpGet("{id}")]
    [Authorize(Policy = "hse.read")]
    public async Task<ActionResult<ApiResponse<PpeIssueReadDto>>> GetById(string id)
    {
        var issue = await ppeIssues.GetByIdAsync(id);
        if (issue == null) return NotFound(ApiResponse<PpeIssueReadDto>.Fail("PPE issue not found.", 404));
        return Ok(ApiResponse<PpeIssueReadDto>.Ok(ToReadDto(issue)));
    }

    [HttpPost]
    [Authorize(Policy = "hse.write")]
    public async Task<ActionResult<ApiResponse<PpeIssueReadDto>>> Create([FromBody] CreatePpeIssueDto dto)
    {
        var issue = new PpeIssue
        {
            EmployeeUserId = dto.EmployeeUserId,
            EmployeeName = dto.EmployeeName,
            Item = dto.Item,
            Condition = dto.Condition,
            ReplacementDueAt = dto.ReplacementDueAt,
        };
        await ppeIssues.CreateAsync(issue);
        return CreatedAtAction(nameof(GetById), new { id = issue.Id, version = "1" },
            ApiResponse<PpeIssueReadDto>.Ok(ToReadDto(issue), "PPE issue recorded."));
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "hse.write")]
    public async Task<ActionResult<ApiResponse<PpeIssueReadDto>>> Update(string id, [FromBody] UpdatePpeIssueDto dto)
    {
        var issue = await ppeIssues.GetByIdAsync(id);
        if (issue == null) return NotFound(ApiResponse<PpeIssueReadDto>.Fail("PPE issue not found.", 404));

        issue.Condition = dto.Condition;
        issue.ReturnedAt = dto.ReturnedAt;
        issue.ReplacementDueAt = dto.ReplacementDueAt;
        await ppeIssues.UpdateAsync(issue);
        return Ok(ApiResponse<PpeIssueReadDto>.Ok(ToReadDto(issue), "PPE issue updated."));
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "hse.delete")]
    public async Task<ActionResult<ApiResponse>> Delete(string id)
    {
        var deleted = await ppeIssues.DeleteAsync(id);
        if (!deleted) return NotFound(new ApiResponse { Success = false, Message = "PPE issue not found.", StatusCode = 404 });
        return Ok(new ApiResponse { Success = true, Message = "PPE issue deleted successfully.", StatusCode = 200 });
    }

    private static PpeIssueReadDto ToReadDto(PpeIssue p) => new()
    {
        Id = p.Id,
        EmployeeUserId = p.EmployeeUserId,
        EmployeeName = p.EmployeeName,
        Item = p.Item,
        Condition = p.Condition,
        IssuedAt = p.IssuedAt,
        ReturnedAt = p.ReturnedAt,
        ReplacementDueAt = p.ReplacementDueAt,
    };
}
