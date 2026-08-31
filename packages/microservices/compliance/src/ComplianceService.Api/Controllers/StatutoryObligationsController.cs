using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ComplianceService.Core.DTOs.Common;
using ComplianceService.Core.DTOs.Statutory;
using ComplianceService.Core.Entities;
using ComplianceService.Core.Interfaces.Repositories;
using ComplianceService.Core.Interfaces.Services;

namespace ComplianceService.Api.Controllers;

// STAT-001: statutory calendar — all recurring compliance deadlines (PAYE 9th monthly, VAT 20th
// monthly, NSSF/SHA 9th monthly, WHT 20th monthly, annual returns, corporate/instalment tax).
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/statutory-obligations")]
public class StatutoryObligationsController(
    IComplianceCrudService<StatutoryObligation> obligations,
    IStatutoryObligationRepository obligationsRepository) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "statutory.read")]
    public async Task<ActionResult<ApiResponse<PaginatedResult<StatutoryObligationReadDto>>>> GetAll([FromQuery] PaginationParameters parameters)
    {
        var paged = await obligationsRepository.GetPagedAsync(parameters);
        var result = new PaginatedResult<StatutoryObligationReadDto>
        {
            Items = paged.Items.Select(ToReadDto).ToList(),
            TotalCount = paged.TotalCount,
            Page = paged.Page,
            PageSize = paged.PageSize
        };
        return Ok(ApiResponse<PaginatedResult<StatutoryObligationReadDto>>.Ok(result));
    }

    [HttpGet("{id}")]
    [Authorize(Policy = "statutory.read")]
    public async Task<ActionResult<ApiResponse<StatutoryObligationReadDto>>> GetById(string id)
    {
        var o = await obligations.GetByIdAsync(id);
        if (o == null) return NotFound(ApiResponse<StatutoryObligationReadDto>.Fail("Obligation not found.", 404));
        return Ok(ApiResponse<StatutoryObligationReadDto>.Ok(ToReadDto(o)));
    }

    [HttpPost]
    [Authorize(Policy = "statutory.write")]
    public async Task<ActionResult<ApiResponse<StatutoryObligationReadDto>>> Create([FromBody] CreateStatutoryObligationDto dto)
    {
        var o = new StatutoryObligation
        {
            Name = dto.Name,
            Authority = dto.Authority,
            Frequency = dto.Frequency,
            StatutoryDay = dto.StatutoryDay,
            OwnerUserId = dto.OwnerUserId,
            OwnerName = dto.OwnerName,
        };
        await obligations.CreateAsync(o);
        return CreatedAtAction(nameof(GetById), new { id = o.Id, version = "1" },
            ApiResponse<StatutoryObligationReadDto>.Ok(ToReadDto(o), "Statutory obligation added."));
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "statutory.write")]
    public async Task<ActionResult<ApiResponse<StatutoryObligationReadDto>>> Update(string id, [FromBody] UpdateStatutoryObligationDto dto)
    {
        var o = await obligations.GetByIdAsync(id);
        if (o == null) return NotFound(ApiResponse<StatutoryObligationReadDto>.Fail("Obligation not found.", 404));

        o.Name = dto.Name;
        o.Authority = dto.Authority;
        o.Frequency = dto.Frequency;
        o.StatutoryDay = dto.StatutoryDay;
        o.OwnerUserId = dto.OwnerUserId;
        o.OwnerName = dto.OwnerName;
        await obligations.UpdateAsync(o);

        return Ok(ApiResponse<StatutoryObligationReadDto>.Ok(ToReadDto(o), "Obligation updated."));
    }

    private static StatutoryObligationReadDto ToReadDto(StatutoryObligation o) => new()
    {
        Id = o.Id,
        Name = o.Name,
        Authority = o.Authority,
        Frequency = o.Frequency,
        StatutoryDay = o.StatutoryDay,
        OwnerUserId = o.OwnerUserId,
        OwnerName = o.OwnerName,
        IsActive = o.IsActive,
    };
}
