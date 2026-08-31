using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ComplianceService.Core.DTOs.Common;
using ComplianceService.Core.DTOs.Icm;
using ComplianceService.Core.Entities;
using ComplianceService.Core.Interfaces.Repositories;
using ComplianceService.Core.Interfaces.Services;

namespace ComplianceService.Api.Controllers;

// ICM-002/003: Inter-Company Services Agreements — scope and recharge rate per related party.
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/compliance-icm-agreements")]
public class IcsaController(
    IComplianceCrudService<Icsa> agreements,
    IComplianceCrudService<RelatedParty> parties,
    IIcsaRepository agreementsRepository) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "compliance.read")]
    public async Task<ActionResult<ApiResponse<PaginatedResult<IcsaReadDto>>>> GetAll([FromQuery] IcsaFilterParameters parameters)
    {
        var paged = await agreementsRepository.GetPagedAsync(parameters);
        var names = (await parties.GetAllAsync()).ToDictionary(p => p.Id, p => p.CompanyName);
        var result = new PaginatedResult<IcsaReadDto>
        {
            Items = paged.Items.Select(a => ToReadDto(a, names)).ToList(),
            TotalCount = paged.TotalCount,
            Page = paged.Page,
            PageSize = paged.PageSize
        };
        return Ok(ApiResponse<PaginatedResult<IcsaReadDto>>.Ok(result));
    }

    [HttpGet("{id}")]
    [Authorize(Policy = "compliance.read")]
    public async Task<ActionResult<ApiResponse<IcsaReadDto>>> GetById(string id)
    {
        var a = await agreements.GetByIdAsync(id);
        if (a == null) return NotFound(ApiResponse<IcsaReadDto>.Fail("Agreement not found.", 404));
        var names = (await parties.GetAllAsync()).ToDictionary(p => p.Id, p => p.CompanyName);
        return Ok(ApiResponse<IcsaReadDto>.Ok(ToReadDto(a, names)));
    }

    [HttpPost]
    [Authorize(Policy = "compliance.write")]
    public async Task<ActionResult<ApiResponse<IcsaReadDto>>> Create([FromBody] CreateIcsaDto dto)
    {
        var party = await parties.GetByIdAsync(dto.RelatedPartyId);
        if (party == null) return NotFound(ApiResponse<IcsaReadDto>.Fail("Related party not found.", 404));

        var a = new Icsa
        {
            RelatedPartyId = dto.RelatedPartyId,
            Scope = dto.Scope,
            RechargeRate = dto.RechargeRate,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
        };
        await agreements.CreateAsync(a);
        var names = new Dictionary<string, string> { [party.Id] = party.CompanyName };
        return CreatedAtAction(nameof(GetById), new { id = a.Id, version = "1" },
            ApiResponse<IcsaReadDto>.Ok(ToReadDto(a, names), "Inter-company services agreement created."));
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "compliance.write")]
    public async Task<ActionResult<ApiResponse<IcsaReadDto>>> Update(string id, [FromBody] UpdateIcsaDto dto)
    {
        var a = await agreements.GetByIdAsync(id);
        if (a == null) return NotFound(ApiResponse<IcsaReadDto>.Fail("Agreement not found.", 404));
        var party = await parties.GetByIdAsync(dto.RelatedPartyId);
        if (party == null) return NotFound(ApiResponse<IcsaReadDto>.Fail("Related party not found.", 404));

        a.RelatedPartyId = dto.RelatedPartyId;
        a.Scope = dto.Scope;
        a.RechargeRate = dto.RechargeRate;
        a.StartDate = dto.StartDate;
        a.EndDate = dto.EndDate;
        await agreements.UpdateAsync(a);

        var names = new Dictionary<string, string> { [party.Id] = party.CompanyName };
        return Ok(ApiResponse<IcsaReadDto>.Ok(ToReadDto(a, names), "Agreement updated."));
    }

    private static IcsaReadDto ToReadDto(Icsa a, Dictionary<string, string> names)
    {
        names.TryGetValue(a.RelatedPartyId, out var name);
        return new IcsaReadDto
        {
            Id = a.Id,
            RelatedPartyId = a.RelatedPartyId,
            RelatedPartyName = name,
            Scope = a.Scope,
            RechargeRate = a.RechargeRate,
            StartDate = a.StartDate,
            EndDate = a.EndDate,
            IsActive = a.EndDate == null || a.EndDate >= DateTime.UtcNow,
        };
    }
}
