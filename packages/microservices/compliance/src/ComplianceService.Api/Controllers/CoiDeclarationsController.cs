using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ComplianceService.Core.DTOs.Coi;
using ComplianceService.Core.DTOs.Common;
using ComplianceService.Core.Entities;
using ComplianceService.Core.Enums;
using ComplianceService.Core.Interfaces.Repositories;
using ComplianceService.Core.Interfaces.Services;

namespace ComplianceService.Api.Controllers;

// COMP-002: annual conflict-of-interest declarations by Department Heads and above;
// reminder workflow in January (see ComplianceAlertsBackgroundService).
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/compliance-coi")]
public class CoiDeclarationsController(
    IComplianceCrudService<CoiDeclaration> coiDeclarations,
    ICoiDeclarationRepository coiDeclarationsRepository) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "compliance.read")]
    public async Task<ActionResult<ApiResponse<PaginatedResult<CoiDeclarationReadDto>>>> GetAll([FromQuery] CoiDeclarationFilterParameters parameters)
    {
        var paged = await coiDeclarationsRepository.GetPagedAsync(parameters);
        var result = new PaginatedResult<CoiDeclarationReadDto>
        {
            Items = paged.Items.Select(ToReadDto).ToList(),
            TotalCount = paged.TotalCount,
            Page = paged.Page,
            PageSize = paged.PageSize
        };
        return Ok(ApiResponse<PaginatedResult<CoiDeclarationReadDto>>.Ok(result));
    }

    [HttpGet("{id}")]
    [Authorize(Policy = "compliance.read")]
    public async Task<ActionResult<ApiResponse<CoiDeclarationReadDto>>> GetById(string id)
    {
        var declaration = await coiDeclarations.GetByIdAsync(id);
        if (declaration == null) return NotFound(ApiResponse<CoiDeclarationReadDto>.Fail("Declaration not found.", 404));
        return Ok(ApiResponse<CoiDeclarationReadDto>.Ok(ToReadDto(declaration)));
    }

    [HttpPost]
    [Authorize(Policy = "compliance.write")]
    public async Task<ActionResult<ApiResponse<CoiDeclarationReadDto>>> Create([FromBody] CreateCoiDeclarationDto dto)
    {
        var declaration = new CoiDeclaration
        {
            EmployeeUserId = dto.EmployeeUserId,
            EmployeeName = dto.EmployeeName,
            Year = dto.Year,
            HasConflict = dto.HasConflict,
            Details = dto.Details,
            DeclaredOn = DateTime.UtcNow,
            Status = CoiStatus.Submitted,
        };
        await coiDeclarations.CreateAsync(declaration);
        return CreatedAtAction(nameof(GetById), new { id = declaration.Id, version = "1" },
            ApiResponse<CoiDeclarationReadDto>.Ok(ToReadDto(declaration), "Declaration submitted."));
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "compliance.write")]
    public async Task<ActionResult<ApiResponse<CoiDeclarationReadDto>>> Update(string id, [FromBody] UpdateCoiDeclarationDto dto)
    {
        var declaration = await coiDeclarations.GetByIdAsync(id);
        if (declaration == null) return NotFound(ApiResponse<CoiDeclarationReadDto>.Fail("Declaration not found.", 404));

        declaration.EmployeeUserId = dto.EmployeeUserId;
        declaration.EmployeeName = dto.EmployeeName;
        declaration.Year = dto.Year;
        declaration.HasConflict = dto.HasConflict;
        declaration.Details = dto.Details;
        await coiDeclarations.UpdateAsync(declaration);

        return Ok(ApiResponse<CoiDeclarationReadDto>.Ok(ToReadDto(declaration), "Declaration updated."));
    }

    [HttpPatch("{id}/review")]
    [Authorize(Policy = "compliance.approve")]
    public async Task<ActionResult<ApiResponse<CoiDeclarationReadDto>>> Review(string id, [FromBody] ReviewCoiDeclarationDto dto)
    {
        var declaration = await coiDeclarations.GetByIdAsync(id);
        if (declaration == null) return NotFound(ApiResponse<CoiDeclarationReadDto>.Fail("Declaration not found.", 404));

        declaration.Status = dto.Status;
        await coiDeclarations.UpdateAsync(declaration);
        return Ok(ApiResponse<CoiDeclarationReadDto>.Ok(ToReadDto(declaration), "Declaration reviewed."));
    }

    private static CoiDeclarationReadDto ToReadDto(CoiDeclaration c) => new()
    {
        Id = c.Id,
        EmployeeUserId = c.EmployeeUserId,
        EmployeeName = c.EmployeeName,
        Year = c.Year,
        DeclaredOn = c.DeclaredOn,
        HasConflict = c.HasConflict,
        Details = c.Details,
        Status = c.Status,
    };
}
