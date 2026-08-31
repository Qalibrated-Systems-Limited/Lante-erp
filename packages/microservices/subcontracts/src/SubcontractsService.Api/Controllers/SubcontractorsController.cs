using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SubcontractsService.Core.DTOs.Common;
using SubcontractsService.Core.DTOs.Subcontractors;
using SubcontractsService.Core.Entities;
using SubcontractsService.Core.Interfaces.Services;

namespace SubcontractsService.Api.Controllers;

// SUB-001: Approved Subcontractor Register (ASR) — searchable by trade category.
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/subcontractors")]
public class SubcontractorsController(ISubcontractsCrudService<Subcontractor> subcontractors) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "subcontracts.read")]
    public async Task<ActionResult<ApiResponse<PaginatedResult<SubcontractorReadDto>>>> GetAll(
        [FromQuery] string? tradeCategory, [FromQuery] PaginationParameters parameters)
    {
        PaginatedResult<SubcontractorReadDto> result;
        if (string.IsNullOrWhiteSpace(tradeCategory))
        {
            var paged = await subcontractors.GetPagedAsync(parameters);
            result = new PaginatedResult<SubcontractorReadDto>
            {
                Items = paged.Items.Select(ToReadDto).ToList(),
                TotalCount = paged.TotalCount,
                Page = paged.Page,
                PageSize = paged.PageSize,
            };
        }
        else
        {
            var filtered = (await subcontractors.GetAllAsync())
                .Where(s => s.TradeCategory.Equals(tradeCategory, StringComparison.OrdinalIgnoreCase))
                .ToList();
            result = new PaginatedResult<SubcontractorReadDto>
            {
                Items = filtered.Skip((parameters.Page - 1) * parameters.PageSize).Take(parameters.PageSize).Select(ToReadDto).ToList(),
                TotalCount = filtered.Count,
                Page = parameters.Page,
                PageSize = parameters.PageSize,
            };
        }
        return Ok(ApiResponse<PaginatedResult<SubcontractorReadDto>>.Ok(result));
    }

    [HttpGet("{id}")]
    [Authorize(Policy = "subcontracts.read")]
    public async Task<ActionResult<ApiResponse<SubcontractorReadDto>>> GetById(string id)
    {
        var subcontractor = await subcontractors.GetByIdAsync(id);
        if (subcontractor == null) return NotFound(ApiResponse<SubcontractorReadDto>.Fail("Subcontractor not found.", 404));
        return Ok(ApiResponse<SubcontractorReadDto>.Ok(ToReadDto(subcontractor)));
    }

    [HttpPost]
    [Authorize(Policy = "subcontracts.write")]
    public async Task<ActionResult<ApiResponse<SubcontractorReadDto>>> Create([FromBody] CreateSubcontractorDto dto)
    {
        var subcontractor = new Subcontractor
        {
            Name = dto.Name,
            TradeCategory = dto.TradeCategory,
            InsuranceExpiry = dto.InsuranceExpiry,
            TccExpiry = dto.TccExpiry,
            HasDeclaredRelationship = dto.HasDeclaredRelationship,
            RelationshipDetails = dto.RelationshipDetails,
            Notes = dto.Notes,
        };
        await subcontractors.CreateAsync(subcontractor);
        return CreatedAtAction(nameof(GetById), new { id = subcontractor.Id, version = "1" },
            ApiResponse<SubcontractorReadDto>.Ok(ToReadDto(subcontractor), "Subcontractor added to the ASR."));
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "subcontracts.write")]
    public async Task<ActionResult<ApiResponse<SubcontractorReadDto>>> Update(string id, [FromBody] UpdateSubcontractorDto dto)
    {
        var subcontractor = await subcontractors.GetByIdAsync(id);
        if (subcontractor == null) return NotFound(ApiResponse<SubcontractorReadDto>.Fail("Subcontractor not found.", 404));

        subcontractor.Name = dto.Name;
        subcontractor.TradeCategory = dto.TradeCategory;
        subcontractor.InsuranceExpiry = dto.InsuranceExpiry;
        subcontractor.TccExpiry = dto.TccExpiry;
        subcontractor.SafetyScore = dto.SafetyScore;
        subcontractor.RamsSubmitted = dto.RamsSubmitted;
        subcontractor.HasDeclaredRelationship = dto.HasDeclaredRelationship;
        subcontractor.RelationshipDetails = dto.RelationshipDetails;
        subcontractor.Notes = dto.Notes;
        await subcontractors.UpdateAsync(subcontractor);
        return Ok(ApiResponse<SubcontractorReadDto>.Ok(ToReadDto(subcontractor), "Subcontractor updated."));
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "subcontracts.delete")]
    public async Task<ActionResult<ApiResponse>> Delete(string id)
    {
        var deleted = await subcontractors.DeleteAsync(id);
        if (!deleted) return NotFound(new ApiResponse { Success = false, Message = "Subcontractor not found.", StatusCode = 404 });
        return Ok(new ApiResponse { Success = true, Message = "Subcontractor deleted successfully.", StatusCode = 200 });
    }

    private static SubcontractorReadDto ToReadDto(Subcontractor s) => new()
    {
        Id = s.Id,
        Name = s.Name,
        TradeCategory = s.TradeCategory,
        PqqScore = s.PqqScore,
        InsuranceExpiry = s.InsuranceExpiry,
        TccExpiry = s.TccExpiry,
        SafetyScore = s.SafetyScore,
        RamsSubmitted = s.RamsSubmitted,
        Prequalified = s.Prequalified,
        LatestPerformanceScore = s.LatestPerformanceScore,
        WatchListed = s.WatchListed,
        IsRestricted = s.IsRestricted,
        HasDeclaredRelationship = s.HasDeclaredRelationship,
        RelationshipDetails = s.RelationshipDetails,
        Notes = s.Notes,
    };
}
