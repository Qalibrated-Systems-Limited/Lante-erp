using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ComplianceService.Core.DTOs.Common;
using ComplianceService.Core.DTOs.Gifts;
using ComplianceService.Core.Entities;
using ComplianceService.Core.Interfaces.Repositories;
using ComplianceService.Core.Interfaces.Services;

namespace ComplianceService.Api.Controllers;

// COMP-001: log of all gifts/hospitality given or received; flag if value exceeds
// Kshs 5,000 (Kshs 2,000 for government officials).
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/compliance-gifts")]
public class GiftHospitalityController(
    IComplianceCrudService<GiftHospitality> gifts,
    IGiftHospitalityRepository giftsRepository) : ControllerBase
{
    private const decimal ThresholdGeneral = 5000m;
    private const decimal ThresholdGovernment = 2000m;

    [HttpGet]
    [Authorize(Policy = "compliance.read")]
    public async Task<ActionResult<ApiResponse<PaginatedResult<GiftHospitalityReadDto>>>> GetAll([FromQuery] GiftHospitalityFilterParameters parameters)
    {
        var paged = await giftsRepository.GetPagedAsync(parameters);
        var result = new PaginatedResult<GiftHospitalityReadDto>
        {
            Items = paged.Items.Select(ToReadDto).ToList(),
            TotalCount = paged.TotalCount,
            Page = paged.Page,
            PageSize = paged.PageSize
        };
        return Ok(ApiResponse<PaginatedResult<GiftHospitalityReadDto>>.Ok(result));
    }

    [HttpGet("{id}")]
    [Authorize(Policy = "compliance.read")]
    public async Task<ActionResult<ApiResponse<GiftHospitalityReadDto>>> GetById(string id)
    {
        var gift = await gifts.GetByIdAsync(id);
        if (gift == null) return NotFound(ApiResponse<GiftHospitalityReadDto>.Fail("Record not found.", 404));
        return Ok(ApiResponse<GiftHospitalityReadDto>.Ok(ToReadDto(gift)));
    }

    [HttpPost]
    [Authorize(Policy = "compliance.write")]
    public async Task<ActionResult<ApiResponse<GiftHospitalityReadDto>>> Create([FromBody] CreateGiftHospitalityDto dto)
    {
        var threshold = dto.IsGovernmentOfficial ? ThresholdGovernment : ThresholdGeneral;
        var gift = new GiftHospitality
        {
            EmployeeUserId = dto.EmployeeUserId,
            EmployeeName = dto.EmployeeName,
            Direction = dto.Direction,
            CounterpartyName = dto.CounterpartyName,
            IsGovernmentOfficial = dto.IsGovernmentOfficial,
            Description = dto.Description,
            Value = dto.Value,
            Date = dto.Date,
            Flagged = dto.Value > threshold,
        };
        await gifts.CreateAsync(gift);
        return CreatedAtAction(nameof(GetById), new { id = gift.Id, version = "1" },
            ApiResponse<GiftHospitalityReadDto>.Ok(ToReadDto(gift), gift.Flagged ? "Recorded — flagged for review (exceeds threshold)." : "Recorded."));
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "compliance.write")]
    public async Task<ActionResult<ApiResponse<GiftHospitalityReadDto>>> Update(string id, [FromBody] UpdateGiftHospitalityDto dto)
    {
        var gift = await gifts.GetByIdAsync(id);
        if (gift == null) return NotFound(ApiResponse<GiftHospitalityReadDto>.Fail("Record not found.", 404));

        var threshold = dto.IsGovernmentOfficial ? ThresholdGovernment : ThresholdGeneral;
        gift.EmployeeUserId = dto.EmployeeUserId;
        gift.EmployeeName = dto.EmployeeName;
        gift.Direction = dto.Direction;
        gift.CounterpartyName = dto.CounterpartyName;
        gift.IsGovernmentOfficial = dto.IsGovernmentOfficial;
        gift.Description = dto.Description;
        gift.Value = dto.Value;
        gift.Date = dto.Date;
        gift.Flagged = dto.Value > threshold;
        await gifts.UpdateAsync(gift);

        return Ok(ApiResponse<GiftHospitalityReadDto>.Ok(ToReadDto(gift), "Record updated."));
    }

    private static GiftHospitalityReadDto ToReadDto(GiftHospitality g) => new()
    {
        Id = g.Id,
        EmployeeUserId = g.EmployeeUserId,
        EmployeeName = g.EmployeeName,
        Direction = g.Direction,
        CounterpartyName = g.CounterpartyName,
        IsGovernmentOfficial = g.IsGovernmentOfficial,
        Description = g.Description,
        Value = g.Value,
        Date = g.Date,
        Flagged = g.Flagged,
    };
}
