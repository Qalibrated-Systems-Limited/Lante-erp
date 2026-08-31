using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ComplianceService.Core.DTOs.Common;
using ComplianceService.Core.DTOs.Licences;
using ComplianceService.Core.Entities;
using ComplianceService.Core.Interfaces.Repositories;
using ComplianceService.Core.Interfaces.Services;

namespace ComplianceService.Api.Controllers;

// COMP-008 / STAT-003,004,005,007: NCA registration, KRA PIN, NEMA permits, KEBS/NMK and DOSHS
// licences — expiry alerts (see ComplianceAlertsBackgroundService). One shared entity across
// both requirement sets — see RegulatoryLicence.cs.
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/compliance-licences")]
public class RegulatoryLicencesController(
    IComplianceCrudService<RegulatoryLicence> licences,
    IRegulatoryLicenceRepository licencesRepository) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "compliance.read")]
    public async Task<ActionResult<ApiResponse<PaginatedResult<RegulatoryLicenceReadDto>>>> GetAll([FromQuery] RegulatoryLicenceFilterParameters parameters)
    {
        var paged = await licencesRepository.GetPagedAsync(parameters);
        var result = new PaginatedResult<RegulatoryLicenceReadDto>
        {
            Items = paged.Items.Select(ToReadDto).ToList(),
            TotalCount = paged.TotalCount,
            Page = paged.Page,
            PageSize = paged.PageSize
        };
        return Ok(ApiResponse<PaginatedResult<RegulatoryLicenceReadDto>>.Ok(result));
    }

    [HttpGet("{id}")]
    [Authorize(Policy = "compliance.read")]
    public async Task<ActionResult<ApiResponse<RegulatoryLicenceReadDto>>> GetById(string id)
    {
        var l = await licences.GetByIdAsync(id);
        if (l == null) return NotFound(ApiResponse<RegulatoryLicenceReadDto>.Fail("Licence not found.", 404));
        return Ok(ApiResponse<RegulatoryLicenceReadDto>.Ok(ToReadDto(l)));
    }

    [HttpPost]
    [Authorize(Policy = "compliance.write")]
    public async Task<ActionResult<ApiResponse<RegulatoryLicenceReadDto>>> Create([FromBody] CreateRegulatoryLicenceDto dto)
    {
        var l = new RegulatoryLicence
        {
            Type = dto.Type,
            Authority = dto.Authority,
            LicenceNumber = dto.LicenceNumber,
            IssuedOn = dto.IssuedOn,
            ExpiryDate = dto.ExpiryDate,
            AlertDays = dto.AlertDays,
            RenewalRequirements = dto.RenewalRequirements,
        };
        await licences.CreateAsync(l);
        return CreatedAtAction(nameof(GetById), new { id = l.Id, version = "1" },
            ApiResponse<RegulatoryLicenceReadDto>.Ok(ToReadDto(l), "Licence recorded."));
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "compliance.write")]
    public async Task<ActionResult<ApiResponse<RegulatoryLicenceReadDto>>> Update(string id, [FromBody] UpdateRegulatoryLicenceDto dto)
    {
        var l = await licences.GetByIdAsync(id);
        if (l == null) return NotFound(ApiResponse<RegulatoryLicenceReadDto>.Fail("Licence not found.", 404));

        l.Type = dto.Type;
        l.Authority = dto.Authority;
        l.LicenceNumber = dto.LicenceNumber;
        l.IssuedOn = dto.IssuedOn;
        l.ExpiryDate = dto.ExpiryDate;
        l.AlertDays = dto.AlertDays;
        l.RenewalRequirements = dto.RenewalRequirements;
        await licences.UpdateAsync(l);

        return Ok(ApiResponse<RegulatoryLicenceReadDto>.Ok(ToReadDto(l), "Licence updated."));
    }

    // Creates the renewed licence as a NEW row rather than overwriting the current one in place —
    // same append-only convention as HSE's statutory inspections, so the previous licence period
    // (its number, issue date, and expiry) stays queryable instead of being destroyed by the next
    // renewal. The old row is soft-deleted (not left active) so it drops out of GetAll and the
    // expiry-alert scan — left alone it would sit expired forever and re-alert every cycle.
    [HttpPatch("{id}/renew")]
    [Authorize(Policy = "compliance.write")]
    public async Task<ActionResult<ApiResponse<RegulatoryLicenceReadDto>>> Renew(string id, [FromBody] RenewRegulatoryLicenceDto dto)
    {
        var current = await licences.GetByIdAsync(id);
        if (current == null) return NotFound(ApiResponse<RegulatoryLicenceReadDto>.Fail("Licence not found.", 404));

        var renewed = new RegulatoryLicence
        {
            Type = current.Type,
            Authority = current.Authority,
            LicenceNumber = dto.LicenceNumber ?? current.LicenceNumber,
            IssuedOn = dto.IssuedOn,
            ExpiryDate = dto.NewExpiryDate,
            AlertDays = current.AlertDays,
            RenewalRequirements = current.RenewalRequirements,
        };
        await licences.CreateAsync(renewed);
        await licences.DeleteAsync(current.Id);
        return Ok(ApiResponse<RegulatoryLicenceReadDto>.Ok(ToReadDto(renewed), "Licence renewed."));
    }

    private static RegulatoryLicenceReadDto ToReadDto(RegulatoryLicence l) => new()
    {
        Id = l.Id,
        Type = l.Type,
        Authority = l.Authority,
        LicenceNumber = l.LicenceNumber,
        IssuedOn = l.IssuedOn,
        ExpiryDate = l.ExpiryDate,
        AlertDays = l.AlertDays,
        RenewalRequirements = l.RenewalRequirements,
    };
}
