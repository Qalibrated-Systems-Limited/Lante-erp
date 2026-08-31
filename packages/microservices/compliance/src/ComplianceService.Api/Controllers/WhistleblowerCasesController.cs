using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ComplianceService.Core.DTOs.CaseActions;
using ComplianceService.Core.DTOs.Common;
using ComplianceService.Core.DTOs.Whistleblower;
using ComplianceService.Core.Entities;
using ComplianceService.Core.Enums;
using ComplianceService.Core.Interfaces.Repositories;
using ComplianceService.Core.Interfaces.Services;

namespace ComplianceService.Api.Controllers;

// COMP-003: anonymous submission portal; case number; investigation status; outcome
// — RESTRICTED ACCESS. Gated by compliance.whistleblower.read/write, a policy
// isolated from the general compliance.read/write hierarchy (see
// PermissionAuthorizationHandler) so ordinary compliance staff cannot see these.
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/compliance-whistleblower-cases")]
public class WhistleblowerCasesController(
    IComplianceCrudService<WhistleblowerCase> cases,
    IComplianceCrudService<CaseAction> caseActions,
    IWhistleblowerCaseRepository casesRepository) : ControllerBase
{
    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
    private string? CurrentUserName => User.FindFirstValue("name") ?? User.FindFirstValue(ClaimTypes.Name);

    [HttpGet]
    [Authorize(Policy = "compliance.whistleblower.read")]
    public async Task<ActionResult<ApiResponse<PaginatedResult<WhistleblowerCaseReadDto>>>> GetAll([FromQuery] PaginationParameters parameters)
    {
        var paged = await casesRepository.GetPagedAsync(parameters);
        var result = new PaginatedResult<WhistleblowerCaseReadDto>
        {
            Items = paged.Items.Select(ToReadDto).ToList(),
            TotalCount = paged.TotalCount,
            Page = paged.Page,
            PageSize = paged.PageSize
        };
        return Ok(ApiResponse<PaginatedResult<WhistleblowerCaseReadDto>>.Ok(result));
    }

    [HttpGet("{id}")]
    [Authorize(Policy = "compliance.whistleblower.read")]
    public async Task<ActionResult<ApiResponse<WhistleblowerCaseReadDto>>> GetById(string id)
    {
        var c = await cases.GetByIdAsync(id);
        if (c == null) return NotFound(ApiResponse<WhistleblowerCaseReadDto>.Fail("Case not found.", 404));
        return Ok(ApiResponse<WhistleblowerCaseReadDto>.Ok(ToReadDto(c)));
    }

    [HttpPost]
    [Authorize(Policy = "compliance.whistleblower.write")]
    public async Task<ActionResult<ApiResponse<WhistleblowerCaseReadDto>>> Create([FromBody] CreateWhistleblowerCaseDto dto)
    {
        var refNo = $"WB-{DateTime.UtcNow:yyyy}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
        var c = new WhistleblowerCase
        {
            RefNo = refNo,
            Anonymous = dto.Anonymous,
            SubmittedByUserId = dto.Anonymous ? null : CurrentUserId,
            Summary = dto.Summary,
        };
        await cases.CreateAsync(c);
        return CreatedAtAction(nameof(GetById), new { id = c.Id, version = "1" },
            ApiResponse<WhistleblowerCaseReadDto>.Ok(ToReadDto(c), $"Case {refNo} submitted."));
    }

    [HttpPatch("{id}")]
    [Authorize(Policy = "compliance.whistleblower.write")]
    public async Task<ActionResult<ApiResponse<WhistleblowerCaseReadDto>>> Update(string id, [FromBody] UpdateWhistleblowerCaseDto dto)
    {
        var c = await cases.GetByIdAsync(id);
        if (c == null) return NotFound(ApiResponse<WhistleblowerCaseReadDto>.Fail("Case not found.", 404));

        c.Status = dto.Status;
        c.Outcome = dto.Outcome;
        await cases.UpdateAsync(c);
        return Ok(ApiResponse<WhistleblowerCaseReadDto>.Ok(ToReadDto(c), "Case updated."));
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "compliance.whistleblower.write")]
    public async Task<ActionResult<ApiResponse<WhistleblowerCaseReadDto>>> UpdateDetails(string id, [FromBody] UpdateWhistleblowerCaseDetailsDto dto)
    {
        var c = await cases.GetByIdAsync(id);
        if (c == null) return NotFound(ApiResponse<WhistleblowerCaseReadDto>.Fail("Case not found.", 404));

        c.Anonymous = dto.Anonymous;
        c.Summary = dto.Summary;
        await cases.UpdateAsync(c);
        return Ok(ApiResponse<WhistleblowerCaseReadDto>.Ok(ToReadDto(c), "Case updated."));
    }

    [HttpGet("{id}/actions")]
    [Authorize(Policy = "compliance.whistleblower.read")]
    public async Task<ActionResult<ApiResponse<IEnumerable<CaseActionReadDto>>>> GetActions(string id)
    {
        var actions = await caseActions.FindAsync(a => a.ParentType == CaseActionParentType.WhistleblowerCase && a.ParentId == id);
        return Ok(ApiResponse<IEnumerable<CaseActionReadDto>>.Ok(actions.OrderBy(a => a.LoggedAt).Select(ToActionDto)));
    }

    [HttpPost("{id}/actions")]
    [Authorize(Policy = "compliance.whistleblower.write")]
    public async Task<ActionResult<ApiResponse<CaseActionReadDto>>> AddAction(string id, [FromBody] CreateCaseActionDto dto)
    {
        var action = new CaseAction
        {
            ParentType = CaseActionParentType.WhistleblowerCase,
            ParentId = id,
            Note = dto.Note,
            LoggedByUserId = CurrentUserId,
            LoggedByName = CurrentUserName,
        };
        await caseActions.CreateAsync(action);
        return Ok(ApiResponse<CaseActionReadDto>.Ok(ToActionDto(action), "Investigation step logged."));
    }

    private static WhistleblowerCaseReadDto ToReadDto(WhistleblowerCase c) => new()
    {
        Id = c.Id,
        RefNo = c.RefNo,
        Anonymous = c.Anonymous,
        Summary = c.Summary,
        Status = c.Status,
        Outcome = c.Outcome,
        CreatedAt = c.CreatedAt,
    };

    private static CaseActionReadDto ToActionDto(CaseAction a) => new()
    {
        Id = a.Id,
        ParentType = a.ParentType,
        ParentId = a.ParentId,
        Note = a.Note,
        LoggedByName = a.LoggedByName,
        LoggedAt = a.LoggedAt,
    };
}
