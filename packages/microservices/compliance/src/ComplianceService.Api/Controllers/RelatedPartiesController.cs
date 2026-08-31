using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ComplianceService.Core.DTOs.Common;
using ComplianceService.Core.DTOs.Icm;
using ComplianceService.Core.Entities;
using ComplianceService.Core.Interfaces.Repositories;
using ComplianceService.Core.Interfaces.Services;

namespace ComplianceService.Api.Controllers;

// ICM-001: register of sister companies / affiliates that ICSAs and intercompany
// transactions are posted against.
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/compliance-icm-related-parties")]
public class RelatedPartiesController(
    IComplianceCrudService<RelatedParty> parties,
    IRelatedPartyRepository partiesRepository) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "compliance.read")]
    public async Task<ActionResult<ApiResponse<PaginatedResult<RelatedPartyReadDto>>>> GetAll([FromQuery] PaginationParameters parameters)
    {
        var paged = await partiesRepository.GetPagedAsync(parameters);
        var result = new PaginatedResult<RelatedPartyReadDto>
        {
            Items = paged.Items.Select(ToReadDto).ToList(),
            TotalCount = paged.TotalCount,
            Page = paged.Page,
            PageSize = paged.PageSize
        };
        return Ok(ApiResponse<PaginatedResult<RelatedPartyReadDto>>.Ok(result));
    }

    [HttpGet("{id}")]
    [Authorize(Policy = "compliance.read")]
    public async Task<ActionResult<ApiResponse<RelatedPartyReadDto>>> GetById(string id)
    {
        var p = await parties.GetByIdAsync(id);
        if (p == null) return NotFound(ApiResponse<RelatedPartyReadDto>.Fail("Related party not found.", 404));
        return Ok(ApiResponse<RelatedPartyReadDto>.Ok(ToReadDto(p)));
    }

    [HttpPost]
    [Authorize(Policy = "compliance.write")]
    public async Task<ActionResult<ApiResponse<RelatedPartyReadDto>>> Create([FromBody] CreateRelatedPartyDto dto)
    {
        var p = new RelatedParty
        {
            CompanyName = dto.CompanyName,
            RegNo = dto.RegNo,
            Relationship = dto.Relationship,
            Notes = dto.Notes,
        };
        await parties.CreateAsync(p);
        return CreatedAtAction(nameof(GetById), new { id = p.Id, version = "1" },
            ApiResponse<RelatedPartyReadDto>.Ok(ToReadDto(p), "Related party registered."));
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "compliance.write")]
    public async Task<ActionResult<ApiResponse<RelatedPartyReadDto>>> Update(string id, [FromBody] UpdateRelatedPartyDto dto)
    {
        var p = await parties.GetByIdAsync(id);
        if (p == null) return NotFound(ApiResponse<RelatedPartyReadDto>.Fail("Related party not found.", 404));

        p.CompanyName = dto.CompanyName;
        p.RegNo = dto.RegNo;
        p.Relationship = dto.Relationship;
        p.Notes = dto.Notes;
        await parties.UpdateAsync(p);

        return Ok(ApiResponse<RelatedPartyReadDto>.Ok(ToReadDto(p), "Related party updated."));
    }

    private static RelatedPartyReadDto ToReadDto(RelatedParty p) => new()
    {
        Id = p.Id, CompanyName = p.CompanyName, RegNo = p.RegNo, Relationship = p.Relationship, Notes = p.Notes,
    };
}
