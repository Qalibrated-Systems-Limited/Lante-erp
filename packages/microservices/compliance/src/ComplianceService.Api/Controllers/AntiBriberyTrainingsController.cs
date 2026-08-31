using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ComplianceService.Core.DTOs.Common;
using ComplianceService.Core.DTOs.Training;
using ComplianceService.Core.Entities;
using ComplianceService.Core.Interfaces.Repositories;
using ComplianceService.Core.Interfaces.Services;

namespace ComplianceService.Api.Controllers;

// COMP-009: staff completion records; 2-year renewal cycle. NextDueOn is computed
// server-side at creation so the renewal alert job (see
// ComplianceAlertsBackgroundService) always has an authoritative due date.
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/compliance-abc-training")]
public class AntiBriberyTrainingsController(
    IComplianceCrudService<AntiBriberyTraining> trainings,
    IAntiBriberyTrainingRepository trainingsRepository) : ControllerBase
{
    private const int RenewalYears = 2;

    [HttpGet]
    [Authorize(Policy = "compliance.read")]
    public async Task<ActionResult<ApiResponse<PaginatedResult<AntiBriberyTrainingReadDto>>>> GetAll([FromQuery] AntiBriberyTrainingFilterParameters parameters)
    {
        var paged = await trainingsRepository.GetPagedAsync(parameters);
        var result = new PaginatedResult<AntiBriberyTrainingReadDto>
        {
            Items = paged.Items.Select(ToReadDto).ToList(),
            TotalCount = paged.TotalCount,
            Page = paged.Page,
            PageSize = paged.PageSize
        };
        return Ok(ApiResponse<PaginatedResult<AntiBriberyTrainingReadDto>>.Ok(result));
    }

    [HttpGet("{id}")]
    [Authorize(Policy = "compliance.read")]
    public async Task<ActionResult<ApiResponse<AntiBriberyTrainingReadDto>>> GetById(string id)
    {
        var t = await trainings.GetByIdAsync(id);
        if (t == null) return NotFound(ApiResponse<AntiBriberyTrainingReadDto>.Fail("Training record not found.", 404));
        return Ok(ApiResponse<AntiBriberyTrainingReadDto>.Ok(ToReadDto(t)));
    }

    [HttpPost]
    [Authorize(Policy = "compliance.write")]
    public async Task<ActionResult<ApiResponse<AntiBriberyTrainingReadDto>>> Create([FromBody] CreateAntiBriberyTrainingDto dto)
    {
        var t = new AntiBriberyTraining
        {
            EmployeeUserId = dto.EmployeeUserId,
            EmployeeName = dto.EmployeeName,
            CompletedOn = dto.CompletedOn,
            NextDueOn = dto.CompletedOn.AddYears(RenewalYears),
            CertificateUrl = dto.CertificateUrl,
        };
        await trainings.CreateAsync(t);
        return CreatedAtAction(nameof(GetById), new { id = t.Id, version = "1" },
            ApiResponse<AntiBriberyTrainingReadDto>.Ok(ToReadDto(t), "Training record added."));
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "compliance.write")]
    public async Task<ActionResult<ApiResponse<AntiBriberyTrainingReadDto>>> Update(string id, [FromBody] UpdateAntiBriberyTrainingDto dto)
    {
        var t = await trainings.GetByIdAsync(id);
        if (t == null) return NotFound(ApiResponse<AntiBriberyTrainingReadDto>.Fail("Training record not found.", 404));

        t.CompletedOn = dto.CompletedOn;
        t.NextDueOn = dto.CompletedOn.AddYears(RenewalYears);
        t.CertificateUrl = dto.CertificateUrl;
        await trainings.UpdateAsync(t);

        return Ok(ApiResponse<AntiBriberyTrainingReadDto>.Ok(ToReadDto(t), "Training record updated."));
    }

    private static AntiBriberyTrainingReadDto ToReadDto(AntiBriberyTraining t) => new()
    {
        Id = t.Id,
        EmployeeUserId = t.EmployeeUserId,
        EmployeeName = t.EmployeeName,
        CompletedOn = t.CompletedOn,
        NextDueOn = t.NextDueOn,
        CertificateUrl = t.CertificateUrl,
    };
}
