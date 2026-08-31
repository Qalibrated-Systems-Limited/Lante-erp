using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ComplianceService.Core.DTOs.Common;
using ComplianceService.Core.DTOs.SopLibrary;
using ComplianceService.Core.Entities;
using ComplianceService.Core.Interfaces.Services;
using ComplianceService.Infrastructure.Services;

namespace ComplianceService.Api.Controllers;

// Quality module's SOP Library. Code (e.g. "QSL/QP/19") is always generated server-side by
// ISopLibraryService — never accepted from the client — so Create goes through that dedicated
// service instead of the generic IComplianceCrudService<T>.CreateAsync every other entity uses.
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/sop-library")]
public class SopLibraryController(
    IComplianceCrudService<SopDocument> sops,
    ISopLibraryService sopLibraryService) : ControllerBase
{
    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    [HttpGet]
    [Authorize(Policy = "compliance.read")]
    public async Task<ActionResult<ApiResponse<PaginatedResult<SopReadDto>>>> GetAll([FromQuery] PaginationParameters parameters)
    {
        var paged = await sops.GetPagedAsync(parameters);
        var result = new PaginatedResult<SopReadDto>
        {
            Items = paged.Items.Select(ToReadDto).ToList(),
            TotalCount = paged.TotalCount,
            Page = paged.Page,
            PageSize = paged.PageSize
        };
        return Ok(ApiResponse<PaginatedResult<SopReadDto>>.Ok(result));
    }

    [HttpGet("{id}")]
    [Authorize(Policy = "compliance.read")]
    public async Task<ActionResult<ApiResponse<SopReadDto>>> GetById(string id)
    {
        var s = await sops.GetByIdAsync(id);
        if (s == null) return NotFound(ApiResponse<SopReadDto>.Fail("SOP not found.", 404));
        return Ok(ApiResponse<SopReadDto>.Ok(ToReadDto(s)));
    }

    [HttpPost]
    [Authorize(Policy = "compliance.write")]
    public async Task<ActionResult<ApiResponse<SopReadDto>>> Create([FromBody] CreateSopDto dto)
    {
        var s = await sopLibraryService.CreateAsync(dto, CurrentUserId);
        return CreatedAtAction(nameof(GetById), new { id = s.Id, version = "1" },
            ApiResponse<SopReadDto>.Ok(ToReadDto(s), "SOP added to the library at Rev 1."));
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "compliance.write")]
    public async Task<ActionResult<ApiResponse<SopReadDto>>> Update(string id, [FromBody] UpdateSopDto dto)
    {
        var s = await sops.GetByIdAsync(id);
        if (s == null) return NotFound(ApiResponse<SopReadDto>.Fail("SOP not found.", 404));

        s.Title = dto.Title;
        s.Department = dto.Department;
        s.Category = dto.Category;
        s.NextReview = dto.NextReview;
        s.FileUrl = dto.FileUrl ?? s.FileUrl;
        if (!string.IsNullOrWhiteSpace(dto.Version)) s.Version = dto.Version;
        s.LastReviewed = DateTime.UtcNow;

        await sops.UpdateAsync(s);
        return Ok(ApiResponse<SopReadDto>.Ok(ToReadDto(s), "SOP updated."));
    }

    private static SopReadDto ToReadDto(SopDocument s) => new()
    {
        Id = s.Id,
        Code = s.Code,
        Title = s.Title,
        Department = s.Department,
        Category = s.Category,
        Version = s.Version,
        FileUrl = s.FileUrl,
        LastReviewed = s.LastReviewed,
        NextReview = s.NextReview,
    };
}
