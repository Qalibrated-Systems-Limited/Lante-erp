using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HSEService.Core.DTOs.Common;
using HSEService.Core.DTOs.Inspections;
using HSEService.Core.Entities;
using HSEService.Core.Interfaces.Repositories;
using HSEService.Core.Interfaces.Services;

namespace HSEService.Api.Controllers;

// HSE-006: statutory inspection register — scaffolding, lifting equipment, pressure vessels —
// legal inspection due dates. Due-date alerts are handled by HseAlertsBackgroundService.
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/hse-statutory-inspections")]
public class StatutoryInspectionsController(IHseCrudService<StatutoryInspection> inspections, IStatutoryInspectionRepository inspectionRepository) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "hse.read")]
    public async Task<ActionResult<ApiResponse<PaginatedResult<StatutoryInspectionReadDto>>>> GetAll([FromQuery] StatutoryInspectionFilterParameters parameters)
    {
        var paged = await inspectionRepository.GetPagedAsync(parameters);
        var dtoItems = paged.Items.Select(ToReadDto).ToList();
        var result = new PaginatedResult<StatutoryInspectionReadDto>
        {
            Items = dtoItems,
            TotalCount = paged.TotalCount,
            Page = paged.Page,
            PageSize = paged.PageSize
        };
        return Ok(ApiResponse<PaginatedResult<StatutoryInspectionReadDto>>.Ok(result));
    }

    [HttpGet("{id}")]
    [Authorize(Policy = "hse.read")]
    public async Task<ActionResult<ApiResponse<StatutoryInspectionReadDto>>> GetById(string id)
    {
        var inspection = await inspections.GetByIdAsync(id);
        if (inspection == null) return NotFound(ApiResponse<StatutoryInspectionReadDto>.Fail("Inspection not found.", 404));
        return Ok(ApiResponse<StatutoryInspectionReadDto>.Ok(ToReadDto(inspection)));
    }

    [HttpPost]
    [Authorize(Policy = "hse.write")]
    public async Task<ActionResult<ApiResponse<StatutoryInspectionReadDto>>> Create([FromBody] CreateStatutoryInspectionDto dto)
    {
        var inspection = new StatutoryInspection
        {
            SiteId = dto.SiteId,
            SiteName = dto.SiteName,
            Equipment = dto.Equipment,
            DueDate = dto.DueDate,
        };
        await inspections.CreateAsync(inspection);
        return CreatedAtAction(nameof(GetById), new { id = inspection.Id, version = "1" },
            ApiResponse<StatutoryInspectionReadDto>.Ok(ToReadDto(inspection), "Statutory inspection scheduled."));
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "hse.write")]
    public async Task<ActionResult<ApiResponse<StatutoryInspectionReadDto>>> Update(string id, [FromBody] UpdateStatutoryInspectionDto dto)
    {
        var inspection = await inspections.GetByIdAsync(id);
        if (inspection == null) return NotFound(ApiResponse<StatutoryInspectionReadDto>.Fail("Inspection not found.", 404));

        inspection.SiteId = dto.SiteId;
        inspection.SiteName = dto.SiteName;
        inspection.Equipment = dto.Equipment;
        inspection.DueDate = dto.DueDate;
        await inspections.UpdateAsync(inspection);

        return Ok(ApiResponse<StatutoryInspectionReadDto>.Ok(ToReadDto(inspection), "Inspection updated."));
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "hse.delete")]
    public async Task<ActionResult<ApiResponse>> Delete(string id)
    {
        var deleted = await inspections.DeleteAsync(id);
        if (!deleted) return NotFound(new ApiResponse { Success = false, Message = "Inspection not found.", StatusCode = 404 });
        return Ok(new ApiResponse { Success = true, Message = "Inspection deleted successfully.", StatusCode = 200 });
    }

    // Records the result against THIS inspection cycle (closing it out, certificate and all) and
    // schedules the next cycle as a new row — same append-only convention as RAMS versioning, so
    // past inspection results/certificates stay queryable instead of being overwritten in place.
    [HttpPatch("{id}/result")]
    [Authorize(Policy = "hse.approve")]
    public async Task<ActionResult<ApiResponse<StatutoryInspectionReadDto>>> RecordResult(string id, [FromBody] RecordInspectionResultDto dto)
    {
        var inspection = await inspections.GetByIdAsync(id);
        if (inspection == null) return NotFound(ApiResponse<StatutoryInspectionReadDto>.Fail("Inspection not found.", 404));

        inspection.Status = dto.Status;
        inspection.InspectorName = dto.InspectorName;
        inspection.CertificateUrl = dto.CertificateUrl;
        inspection.LastInspectedAt = DateTime.UtcNow;
        await inspections.UpdateAsync(inspection);

        var next = new StatutoryInspection
        {
            SiteId = inspection.SiteId,
            SiteName = inspection.SiteName,
            Equipment = inspection.Equipment,
            DueDate = dto.NextDueDate,
        };
        await inspections.CreateAsync(next);

        return Ok(ApiResponse<StatutoryInspectionReadDto>.Ok(ToReadDto(inspection), "Inspection result recorded; next cycle scheduled."));
    }

    private static StatutoryInspectionReadDto ToReadDto(StatutoryInspection i) => new()
    {
        Id = i.Id,
        SiteId = i.SiteId,
        SiteName = i.SiteName,
        Equipment = i.Equipment,
        InspectorName = i.InspectorName,
        LastInspectedAt = i.LastInspectedAt,
        DueDate = i.DueDate,
        Status = i.Status,
        CertificateUrl = i.CertificateUrl,
    };
}
