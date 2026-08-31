using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ComplianceService.Core.DTOs.CaseActions;
using ComplianceService.Core.DTOs.Common;
using ComplianceService.Core.DTOs.Dsr;
using ComplianceService.Core.Entities;
using ComplianceService.Core.Enums;
using ComplianceService.Core.Interfaces.Repositories;
using ComplianceService.Core.Interfaces.Services;

namespace ComplianceService.Api.Controllers;

// COMP-004: track and respond to access/erasure/correction requests within 30 days
// per DPA 2019. DueBy is computed server-side at creation.
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/compliance-dsr")]
public class DataSubjectRequestsController(
    IComplianceCrudService<DataSubjectRequest> requests,
    IComplianceCrudService<CaseAction> caseActions,
    IDataSubjectRequestRepository requestsRepository) : ControllerBase
{
    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
    private string? CurrentUserName => User.FindFirstValue("name") ?? User.FindFirstValue(ClaimTypes.Name);

    [HttpGet]
    [Authorize(Policy = "compliance.read")]
    public async Task<ActionResult<ApiResponse<PaginatedResult<DataSubjectRequestReadDto>>>> GetAll([FromQuery] PaginationParameters parameters)
    {
        var paged = await requestsRepository.GetPagedAsync(parameters);
        var result = new PaginatedResult<DataSubjectRequestReadDto>
        {
            Items = paged.Items.Select(ToReadDto).ToList(),
            TotalCount = paged.TotalCount,
            Page = paged.Page,
            PageSize = paged.PageSize
        };
        return Ok(ApiResponse<PaginatedResult<DataSubjectRequestReadDto>>.Ok(result));
    }

    [HttpGet("{id}")]
    [Authorize(Policy = "compliance.read")]
    public async Task<ActionResult<ApiResponse<DataSubjectRequestReadDto>>> GetById(string id)
    {
        var r = await requests.GetByIdAsync(id);
        if (r == null) return NotFound(ApiResponse<DataSubjectRequestReadDto>.Fail("Request not found.", 404));
        return Ok(ApiResponse<DataSubjectRequestReadDto>.Ok(ToReadDto(r)));
    }

    [HttpPost]
    [Authorize(Policy = "compliance.write")]
    public async Task<ActionResult<ApiResponse<DataSubjectRequestReadDto>>> Create([FromBody] CreateDataSubjectRequestDto dto)
    {
        var r = new DataSubjectRequest
        {
            Type = dto.Type,
            RequestorName = dto.RequestorName,
            RequestorContact = dto.RequestorContact,
            ReceivedOn = dto.ReceivedOn,
            DueBy = dto.ReceivedOn.AddDays(30),
        };
        await requests.CreateAsync(r);
        return CreatedAtAction(nameof(GetById), new { id = r.Id, version = "1" },
            ApiResponse<DataSubjectRequestReadDto>.Ok(ToReadDto(r), "Request logged — due within 30 days (DPA 2019)."));
    }

    [HttpPatch("{id}")]
    [Authorize(Policy = "compliance.write")]
    public async Task<ActionResult<ApiResponse<DataSubjectRequestReadDto>>> Update(string id, [FromBody] UpdateDataSubjectRequestDto dto)
    {
        var r = await requests.GetByIdAsync(id);
        if (r == null) return NotFound(ApiResponse<DataSubjectRequestReadDto>.Fail("Request not found.", 404));

        r.Status = dto.Status;
        r.Notes = dto.Notes;
        r.CompletedOn = dto.Status == DsrStatus.Completed ? (r.CompletedOn ?? DateTime.UtcNow) : null;
        await requests.UpdateAsync(r);
        return Ok(ApiResponse<DataSubjectRequestReadDto>.Ok(ToReadDto(r), "Request updated."));
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "compliance.write")]
    public async Task<ActionResult<ApiResponse<DataSubjectRequestReadDto>>> UpdateDetails(string id, [FromBody] UpdateDataSubjectRequestDetailsDto dto)
    {
        var r = await requests.GetByIdAsync(id);
        if (r == null) return NotFound(ApiResponse<DataSubjectRequestReadDto>.Fail("Request not found.", 404));

        r.Type = dto.Type;
        r.RequestorName = dto.RequestorName;
        r.RequestorContact = dto.RequestorContact;
        r.ReceivedOn = dto.ReceivedOn;
        r.DueBy = dto.ReceivedOn.AddDays(30);
        await requests.UpdateAsync(r);
        return Ok(ApiResponse<DataSubjectRequestReadDto>.Ok(ToReadDto(r), "Request updated."));
    }

    [HttpGet("{id}/actions")]
    [Authorize(Policy = "compliance.read")]
    public async Task<ActionResult<ApiResponse<IEnumerable<CaseActionReadDto>>>> GetActions(string id)
    {
        var actions = await caseActions.FindAsync(a => a.ParentType == CaseActionParentType.DataSubjectRequest && a.ParentId == id);
        return Ok(ApiResponse<IEnumerable<CaseActionReadDto>>.Ok(actions.OrderBy(a => a.LoggedAt).Select(ToActionDto)));
    }

    [HttpPost("{id}/actions")]
    [Authorize(Policy = "compliance.write")]
    public async Task<ActionResult<ApiResponse<CaseActionReadDto>>> AddAction(string id, [FromBody] CreateCaseActionDto dto)
    {
        var action = new CaseAction
        {
            ParentType = CaseActionParentType.DataSubjectRequest,
            ParentId = id,
            Note = dto.Note,
            LoggedByUserId = CurrentUserId,
            LoggedByName = CurrentUserName,
        };
        await caseActions.CreateAsync(action);
        return Ok(ApiResponse<CaseActionReadDto>.Ok(ToActionDto(action), "Action logged."));
    }

    private static DataSubjectRequestReadDto ToReadDto(DataSubjectRequest r) => new()
    {
        Id = r.Id,
        Type = r.Type,
        RequestorName = r.RequestorName,
        RequestorContact = r.RequestorContact,
        ReceivedOn = r.ReceivedOn,
        DueBy = r.DueBy,
        Status = r.Status,
        CompletedOn = r.CompletedOn,
        Notes = r.Notes,
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
