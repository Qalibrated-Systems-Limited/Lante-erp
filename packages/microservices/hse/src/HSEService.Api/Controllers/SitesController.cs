using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HSEService.Core.DTOs.Common;
using HSEService.Core.DTOs.Sites;
using HSEService.Core.Entities;
using HSEService.Core.Interfaces.Services;

namespace HSEService.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/hse-sites")]
public class SitesController(IHseCrudService<Site> sites) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "hse.read")]
    public async Task<ActionResult<ApiResponse<IEnumerable<SiteReadDto>>>> GetAll()
    {
        var all = await sites.GetAllAsync();
        return Ok(ApiResponse<IEnumerable<SiteReadDto>>.Ok(all.Select(ToReadDto)));
    }

    [HttpGet("{id}")]
    [Authorize(Policy = "hse.read")]
    public async Task<ActionResult<ApiResponse<SiteReadDto>>> GetById(string id)
    {
        var site = await sites.GetByIdAsync(id);
        if (site == null) return NotFound(ApiResponse<SiteReadDto>.Fail("Site not found.", 404));
        return Ok(ApiResponse<SiteReadDto>.Ok(ToReadDto(site)));
    }

    [HttpPost]
    [Authorize(Policy = "hse.write")]
    public async Task<ActionResult<ApiResponse<SiteReadDto>>> Create([FromBody] CreateSiteDto dto)
    {
        var site = new Site
        {
            Name = dto.Name,
            Location = dto.Location,
            ProjectId = dto.ProjectId,
            ProjectName = dto.ProjectName,
        };
        await sites.CreateAsync(site);
        return CreatedAtAction(nameof(GetById), new { id = site.Id, version = "1" },
            ApiResponse<SiteReadDto>.Ok(ToReadDto(site), "Site created successfully."));
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "hse.write")]
    public async Task<ActionResult<ApiResponse<SiteReadDto>>> Update(string id, [FromBody] UpdateSiteDto dto)
    {
        var site = await sites.GetByIdAsync(id);
        if (site == null) return NotFound(ApiResponse<SiteReadDto>.Fail("Site not found.", 404));

        site.Name = dto.Name;
        site.Location = dto.Location;
        site.ProjectId = dto.ProjectId;
        site.ProjectName = dto.ProjectName;
        site.IsActive = dto.IsActive;
        await sites.UpdateAsync(site);
        return Ok(ApiResponse<SiteReadDto>.Ok(ToReadDto(site), "Site updated successfully."));
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "hse.delete")]
    public async Task<ActionResult<ApiResponse>> Delete(string id)
    {
        var deleted = await sites.DeleteAsync(id);
        if (!deleted) return NotFound(new ApiResponse { Success = false, Message = "Site not found.", StatusCode = 404 });
        return Ok(new ApiResponse { Success = true, Message = "Site deleted successfully.", StatusCode = 200 });
    }

    private static SiteReadDto ToReadDto(Site s) => new()
    {
        Id = s.Id,
        Name = s.Name,
        Location = s.Location,
        ProjectId = s.ProjectId,
        ProjectName = s.ProjectName,
        IsActive = s.IsActive,
        CreatedAt = s.CreatedAt,
    };
}
