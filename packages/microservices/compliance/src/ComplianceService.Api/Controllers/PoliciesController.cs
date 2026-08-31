using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ComplianceService.Core.DTOs.Common;
using ComplianceService.Core.DTOs.Policies;
using ComplianceService.Core.Entities;
using ComplianceService.Core.Interfaces.Services;

namespace ComplianceService.Api.Controllers;

// COMP-006: every staff member digitally signs all company policies; confirmation stored.
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/compliance-policies")]
public class PoliciesController(
    IComplianceCrudService<Policy> policies,
    IComplianceCrudService<PolicyAck> acks) : ControllerBase
{
    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
    private string? CurrentUserName => User.FindFirstValue("name") ?? User.FindFirstValue(ClaimTypes.Name);

    [HttpGet]
    [Authorize(Policy = "compliance.read")]
    public async Task<ActionResult<ApiResponse<PaginatedResult<PolicyReadDto>>>> GetAll([FromQuery] PaginationParameters parameters)
    {
        var paged = await policies.GetPagedAsync(parameters);
        var dtos = new List<PolicyReadDto>();
        foreach (var p in paged.Items)
        {
            var ackCount = (await acks.FindAsync(a => a.PolicyId == p.Id)).Count;
            dtos.Add(ToReadDto(p, ackCount));
        }
        var result = new PaginatedResult<PolicyReadDto>
        {
            Items = dtos,
            TotalCount = paged.TotalCount,
            Page = paged.Page,
            PageSize = paged.PageSize
        };
        return Ok(ApiResponse<PaginatedResult<PolicyReadDto>>.Ok(result));
    }

    [HttpGet("{id}")]
    [Authorize(Policy = "compliance.read")]
    public async Task<ActionResult<ApiResponse<PolicyReadDto>>> GetById(string id)
    {
        var p = await policies.GetByIdAsync(id);
        if (p == null) return NotFound(ApiResponse<PolicyReadDto>.Fail("Policy not found.", 404));
        var ackCount = (await acks.FindAsync(a => a.PolicyId == id)).Count;
        return Ok(ApiResponse<PolicyReadDto>.Ok(ToReadDto(p, ackCount)));
    }

    [HttpPost]
    [Authorize(Policy = "compliance.write")]
    public async Task<ActionResult<ApiResponse<PolicyReadDto>>> Create([FromBody] CreatePolicyDto dto)
    {
        var p = new Policy
        {
            Title = dto.Title,
            Version = dto.Version,
            FileUrl = dto.FileUrl,
            PublishedAt = DateTime.UtcNow,
        };
        await policies.CreateAsync(p);
        return CreatedAtAction(nameof(GetById), new { id = p.Id, version = "1" },
            ApiResponse<PolicyReadDto>.Ok(ToReadDto(p, 0), "Policy published."));
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "compliance.write")]
    public async Task<ActionResult<ApiResponse<PolicyReadDto>>> Update(string id, [FromBody] UpdatePolicyDto dto)
    {
        var p = await policies.GetByIdAsync(id);
        if (p == null) return NotFound(ApiResponse<PolicyReadDto>.Fail("Policy not found.", 404));

        p.Title = dto.Title;
        p.Version = dto.Version;
        p.FileUrl = dto.FileUrl;
        await policies.UpdateAsync(p);

        var ackCount = (await acks.FindAsync(a => a.PolicyId == id)).Count;
        return Ok(ApiResponse<PolicyReadDto>.Ok(ToReadDto(p, ackCount), "Policy updated."));
    }

    [HttpGet("{id}/acknowledgements")]
    [Authorize(Policy = "compliance.read")]
    public async Task<ActionResult<ApiResponse<IEnumerable<PolicyAckReadDto>>>> GetAcknowledgements(string id)
    {
        var list = await acks.FindAsync(a => a.PolicyId == id);
        return Ok(ApiResponse<IEnumerable<PolicyAckReadDto>>.Ok(list.OrderBy(a => a.SignedAt).Select(ToAckDto)));
    }

    [HttpPost("{id}/acknowledge")]
    [Authorize(Policy = "compliance.write")]
    public async Task<ActionResult<ApiResponse<PolicyAckReadDto>>> Acknowledge(string id)
    {
        var policy = await policies.GetByIdAsync(id);
        if (policy == null) return NotFound(ApiResponse<PolicyAckReadDto>.Fail("Policy not found.", 404));

        var ack = new PolicyAck
        {
            PolicyId = id,
            EmployeeUserId = CurrentUserId,
            EmployeeName = CurrentUserName,
        };
        await acks.CreateAsync(ack);
        return Ok(ApiResponse<PolicyAckReadDto>.Ok(ToAckDto(ack), "Policy acknowledged."));
    }

    private static PolicyReadDto ToReadDto(Policy p, int ackCount) => new()
    {
        Id = p.Id,
        Title = p.Title,
        Version = p.Version,
        PublishedAt = p.PublishedAt,
        FileUrl = p.FileUrl,
        AcknowledgedCount = ackCount,
    };

    private static PolicyAckReadDto ToAckDto(PolicyAck a) => new()
    {
        Id = a.Id,
        PolicyId = a.PolicyId,
        EmployeeUserId = a.EmployeeUserId,
        EmployeeName = a.EmployeeName,
        SignedAt = a.SignedAt,
    };
}
