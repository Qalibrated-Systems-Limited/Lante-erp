using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ComplianceService.Core.DTOs.BoardResolutions;
using ComplianceService.Core.DTOs.Common;
using ComplianceService.Core.Entities;
using ComplianceService.Core.Interfaces.Repositories;
using ComplianceService.Core.Interfaces.Services;

namespace ComplianceService.Api.Controllers;

// COMP-007: every board resolution logged with date, reference number, and scanned copy.
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/compliance-board-resolutions")]
public class BoardResolutionsController(
    IComplianceCrudService<BoardResolution> resolutions,
    IBoardResolutionRepository resolutionsRepository) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "compliance.read")]
    public async Task<ActionResult<ApiResponse<PaginatedResult<BoardResolutionReadDto>>>> GetAll([FromQuery] PaginationParameters parameters)
    {
        var paged = await resolutionsRepository.GetPagedAsync(parameters);
        var result = new PaginatedResult<BoardResolutionReadDto>
        {
            Items = paged.Items.Select(ToReadDto).ToList(),
            TotalCount = paged.TotalCount,
            Page = paged.Page,
            PageSize = paged.PageSize
        };
        return Ok(ApiResponse<PaginatedResult<BoardResolutionReadDto>>.Ok(result));
    }

    [HttpGet("{id}")]
    [Authorize(Policy = "compliance.read")]
    public async Task<ActionResult<ApiResponse<BoardResolutionReadDto>>> GetById(string id)
    {
        var r = await resolutions.GetByIdAsync(id);
        if (r == null) return NotFound(ApiResponse<BoardResolutionReadDto>.Fail("Resolution not found.", 404));
        return Ok(ApiResponse<BoardResolutionReadDto>.Ok(ToReadDto(r)));
    }

    [HttpPost]
    [Authorize(Policy = "compliance.write")]
    public async Task<ActionResult<ApiResponse<BoardResolutionReadDto>>> Create([FromBody] CreateBoardResolutionDto dto)
    {
        var r = new BoardResolution
        {
            ReferenceNo = dto.ReferenceNo,
            Title = dto.Title,
            ResolutionDate = dto.ResolutionDate,
            Summary = dto.Summary,
            ScannedCopyUrl = dto.ScannedCopyUrl,
        };
        await resolutions.CreateAsync(r);
        return CreatedAtAction(nameof(GetById), new { id = r.Id, version = "1" },
            ApiResponse<BoardResolutionReadDto>.Ok(ToReadDto(r), "Resolution logged."));
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "compliance.write")]
    public async Task<ActionResult<ApiResponse<BoardResolutionReadDto>>> Update(string id, [FromBody] UpdateBoardResolutionDto dto)
    {
        var r = await resolutions.GetByIdAsync(id);
        if (r == null) return NotFound(ApiResponse<BoardResolutionReadDto>.Fail("Resolution not found.", 404));

        r.ReferenceNo = dto.ReferenceNo;
        r.Title = dto.Title;
        r.ResolutionDate = dto.ResolutionDate;
        r.Summary = dto.Summary;
        r.ScannedCopyUrl = dto.ScannedCopyUrl;
        await resolutions.UpdateAsync(r);

        return Ok(ApiResponse<BoardResolutionReadDto>.Ok(ToReadDto(r), "Resolution updated."));
    }

    private static BoardResolutionReadDto ToReadDto(BoardResolution r) => new()
    {
        Id = r.Id,
        ReferenceNo = r.ReferenceNo,
        Title = r.Title,
        ResolutionDate = r.ResolutionDate,
        Summary = r.Summary,
        ScannedCopyUrl = r.ScannedCopyUrl,
    };
}
