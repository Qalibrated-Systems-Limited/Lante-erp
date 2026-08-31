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

// STAT-006: current Tax Compliance Certificate status, expiry date — alert at 60 days;
// auto-remind Finance to renew via KRA iTax (StatutoryCalendarBackgroundService).
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/tax-compliance-certs")]
public class TaxComplianceCertsController(
    IComplianceCrudService<TaxComplianceCert> tccs,
    ITaxComplianceCertRepository tccsRepository,
    IStatutoryDashboardService dashboard) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "statutory.read")]
    public async Task<ActionResult<ApiResponse<PaginatedResult<TaxComplianceCertReadDto>>>> GetAll([FromQuery] PaginationParameters parameters)
    {
        var paged = await tccsRepository.GetPagedAsync(parameters);
        var result = new PaginatedResult<TaxComplianceCertReadDto>
        {
            Items = paged.Items.Select(ToReadDto).ToList(),
            TotalCount = paged.TotalCount,
            Page = paged.Page,
            PageSize = paged.PageSize
        };
        return Ok(ApiResponse<PaginatedResult<TaxComplianceCertReadDto>>.Ok(result));
    }

    [HttpGet("{id}")]
    [Authorize(Policy = "statutory.read")]
    public async Task<ActionResult<ApiResponse<TaxComplianceCertReadDto>>> GetById(string id)
    {
        var t = await tccs.GetByIdAsync(id);
        if (t == null) return NotFound(ApiResponse<TaxComplianceCertReadDto>.Fail("TCC not found.", 404));
        return Ok(ApiResponse<TaxComplianceCertReadDto>.Ok(ToReadDto(t)));
    }

    [HttpPost]
    [Authorize(Policy = "statutory.write")]
    public async Task<ActionResult<ApiResponse<TaxComplianceCertReadDto>>> Create([FromBody] CreateTaxComplianceCertDto dto)
    {
        var t = new TaxComplianceCert { ExpiryDate = dto.ExpiryDate, ItaxRef = dto.ItaxRef, AlertDays = dto.AlertDays };
        await tccs.CreateAsync(t);
        return CreatedAtAction(nameof(GetById), new { id = t.Id, version = "1" },
            ApiResponse<TaxComplianceCertReadDto>.Ok(ToReadDto(t), "TCC recorded."));
    }

    [HttpPatch("{id}/renew")]
    [Authorize(Policy = "statutory.write")]
    public async Task<ActionResult<ApiResponse<TaxComplianceCertReadDto>>> Renew(string id, [FromBody] RenewTaxComplianceCertDto dto)
    {
        var t = await tccs.GetByIdAsync(id);
        if (t == null) return NotFound(ApiResponse<TaxComplianceCertReadDto>.Fail("TCC not found.", 404));

        t.ExpiryDate = dto.NewExpiryDate;
        t.ItaxRef = dto.ItaxRef ?? t.ItaxRef;
        t.Status = TccStatus.Valid;
        await tccs.UpdateAsync(t);
        return Ok(ApiResponse<TaxComplianceCertReadDto>.Ok(ToReadDto(t), "TCC renewed."));
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "statutory.write")]
    public async Task<ActionResult<ApiResponse<TaxComplianceCertReadDto>>> Update(string id, [FromBody] UpdateTaxComplianceCertDto dto)
    {
        var t = await tccs.GetByIdAsync(id);
        if (t == null) return NotFound(ApiResponse<TaxComplianceCertReadDto>.Fail("TCC not found.", 404));

        t.ExpiryDate = dto.ExpiryDate;
        t.ItaxRef = dto.ItaxRef;
        t.AlertDays = dto.AlertDays;
        await tccs.UpdateAsync(t);

        return Ok(ApiResponse<TaxComplianceCertReadDto>.Ok(ToReadDto(t), "TCC updated."));
    }

    private TaxComplianceCertReadDto ToReadDto(TaxComplianceCert t) => new()
    {
        Id = t.Id,
        Status = t.Status,
        ExpiryDate = t.ExpiryDate,
        ItaxRef = t.ItaxRef,
        AlertDays = t.AlertDays,
        Rag = dashboard.ComputeRag(t.ExpiryDate),
    };
}
