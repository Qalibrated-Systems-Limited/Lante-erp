using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ComplianceService.Core.DTOs.Common;
using ComplianceService.Core.DTOs.Statutory;
using ComplianceService.Core.Entities;
using ComplianceService.Core.Enums;
using ComplianceService.Core.Interfaces.Repositories;
using ComplianceService.Core.Interfaces.Services;

namespace ComplianceService.Api.Controllers;

// STAT-002: status of all annual returns with the Registrar of Companies (2017-2025 backlog and
// ongoing) — filed/pending/overdue; 60-day alert to MD and Company Secretary
// (StatutoryCalendarBackgroundService).
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/annual-returns")]
public class AnnualReturnsController(
    IComplianceCrudService<AnnualReturn> returns,
    IAnnualReturnRepository returnsRepository,
    IStatutoryDashboardService dashboard) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "statutory.read")]
    public async Task<ActionResult<ApiResponse<PaginatedResult<AnnualReturnReadDto>>>> GetAll([FromQuery] PaginationParameters parameters)
    {
        var paged = await returnsRepository.GetPagedAsync(parameters);
        var result = new PaginatedResult<AnnualReturnReadDto>
        {
            Items = paged.Items.Select(ToReadDto).ToList(),
            TotalCount = paged.TotalCount,
            Page = paged.Page,
            PageSize = paged.PageSize
        };
        return Ok(ApiResponse<PaginatedResult<AnnualReturnReadDto>>.Ok(result));
    }

    [HttpGet("{id}")]
    [Authorize(Policy = "statutory.read")]
    public async Task<ActionResult<ApiResponse<AnnualReturnReadDto>>> GetById(string id)
    {
        var r = await returns.GetByIdAsync(id);
        if (r == null) return NotFound(ApiResponse<AnnualReturnReadDto>.Fail("Annual return not found.", 404));
        return Ok(ApiResponse<AnnualReturnReadDto>.Ok(ToReadDto(r)));
    }

    [HttpPost]
    [Authorize(Policy = "statutory.write")]
    public async Task<ActionResult<ApiResponse<AnnualReturnReadDto>>> Create([FromBody] CreateAnnualReturnDto dto)
    {
        var r = new AnnualReturn { Year = dto.Year, DueDate = dto.DueDate };
        await returns.CreateAsync(r);
        return CreatedAtAction(nameof(GetById), new { id = r.Id, version = "1" },
            ApiResponse<AnnualReturnReadDto>.Ok(ToReadDto(r), "Annual return record added."));
    }

    [HttpPatch("{id}/file")]
    [Authorize(Policy = "statutory.approve")]
    public async Task<ActionResult<ApiResponse<AnnualReturnReadDto>>> MarkFiled(string id, [FromBody] FileAnnualReturnDto dto)
    {
        var r = await returns.GetByIdAsync(id);
        if (r == null) return NotFound(ApiResponse<AnnualReturnReadDto>.Fail("Annual return not found.", 404));

        r.Status = AnnualReturnStatus.Filed;
        r.FiledDate = dto.FiledDate;
        await returns.UpdateAsync(r);
        return Ok(ApiResponse<AnnualReturnReadDto>.Ok(ToReadDto(r), "Annual return marked as filed."));
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "statutory.write")]
    public async Task<ActionResult<ApiResponse<AnnualReturnReadDto>>> Update(string id, [FromBody] UpdateAnnualReturnDto dto)
    {
        var r = await returns.GetByIdAsync(id);
        if (r == null) return NotFound(ApiResponse<AnnualReturnReadDto>.Fail("Annual return not found.", 404));

        r.Year = dto.Year;
        r.DueDate = dto.DueDate;
        await returns.UpdateAsync(r);

        return Ok(ApiResponse<AnnualReturnReadDto>.Ok(ToReadDto(r), "Annual return updated."));
    }

    private AnnualReturnReadDto ToReadDto(AnnualReturn r) => new()
    {
        Id = r.Id,
        Year = r.Year,
        DueDate = r.DueDate,
        Status = r.Status,
        FiledDate = r.FiledDate,
        Rag = dashboard.ComputeRag(r.DueDate),
    };
}
