using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HSEService.Core.DTOs.Common;
using HSEService.Core.DTOs.Rams;
using HSEService.Core.Entities;
using HSEService.Core.Enums;
using HSEService.Core.Interfaces.Repositories;
using HSEService.Core.Interfaces.Services;

namespace HSEService.Api.Controllers;

// HSE-002: RAMS library — upload, version control, and issue tracking per site/project.
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/hse-rams")]
public class RamsController(IRamsWorkflowService workflow, IHseCrudService<Rams> rams, IRamsRepository ramsRepository, ITicketingServiceClient ticketingClient) : ControllerBase
{
    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
    private string? CurrentTenantSchema => User.FindFirstValue("schema");

    [HttpGet]
    [Authorize(Policy = "hse.read")]
    public async Task<ActionResult<ApiResponse<PaginatedResult<RamsReadDto>>>> GetAll([FromQuery] RamsFilterParameters parameters)
    {
        var paged = await ramsRepository.GetPagedAsync(parameters);
        var dtoItems = paged.Items.Select(ToReadDto).ToList();
        var result = new PaginatedResult<RamsReadDto>
        {
            Items = dtoItems,
            TotalCount = paged.TotalCount,
            Page = paged.Page,
            PageSize = paged.PageSize
        };
        return Ok(ApiResponse<PaginatedResult<RamsReadDto>>.Ok(result));
    }

    [HttpGet("{id}")]
    [Authorize(Policy = "hse.read")]
    public async Task<ActionResult<ApiResponse<RamsReadDto>>> GetById(string id)
    {
        var record = await rams.GetByIdAsync(id);
        if (record == null) return NotFound(ApiResponse<RamsReadDto>.Fail("RAMS record not found.", 404));
        return Ok(ApiResponse<RamsReadDto>.Ok(ToReadDto(record)));
    }

    [HttpPost]
    [Authorize(Policy = "hse.write")]
    public async Task<ActionResult<ApiResponse<RamsReadDto>>> Upload([FromBody] CreateRamsDto dto)
    {
        var record = await workflow.UploadNewVersionAsync(dto, CurrentUserId);
        return CreatedAtAction(nameof(GetById), new { id = record.Id, version = "1" },
            ApiResponse<RamsReadDto>.Ok(ToReadDto(record), $"RAMS v{record.Version} uploaded — pending review."));
    }

    [HttpPut("{id}")]
    [Authorize(Policy = "hse.write")]
    public async Task<ActionResult<ApiResponse<RamsReadDto>>> Update(string id, [FromBody] UpdateRamsDto dto)
    {
        var record = await rams.GetByIdAsync(id);
        if (record == null) return NotFound(ApiResponse<RamsReadDto>.Fail("RAMS record not found.", 404));

        record.SiteId = dto.SiteId;
        record.SiteName = dto.SiteName;
        record.SubcontractorId = dto.SubcontractorId;
        record.SubcontractorName = dto.SubcontractorName;
        record.Title = dto.Title;
        record.FileUrl = dto.FileUrl;
        record.IssueNotes = dto.IssueNotes;
        await rams.UpdateAsync(record);

        return Ok(ApiResponse<RamsReadDto>.Ok(ToReadDto(record), "RAMS record updated."));
    }

    [HttpDelete("{id}")]
    [Authorize(Policy = "hse.delete")]
    public async Task<ActionResult<ApiResponse>> Delete(string id)
    {
        var deleted = await rams.DeleteAsync(id);
        if (!deleted) return NotFound(new ApiResponse { Success = false, Message = "RAMS record not found.", StatusCode = 404 });
        return Ok(new ApiResponse { Success = true, Message = "RAMS record deleted successfully.", StatusCode = 200 });
    }

    [HttpPatch("{id}/status")]
    [Authorize(Policy = "hse.approve")]
    public async Task<ActionResult<ApiResponse<RamsReadDto>>> UpdateStatus(string id, [FromBody] UpdateRamsStatusDto dto)
    {
        var record = await rams.GetByIdAsync(id);
        if (record == null) return NotFound(ApiResponse<RamsReadDto>.Fail("RAMS record not found.", 404));

        record.Status = dto.Status;
        record.IssueNotes = dto.IssueNotes;
        record.ReviewedAt = DateTime.UtcNow;
        await rams.UpdateAsync(record);

        if (!string.IsNullOrWhiteSpace(record.UploadedByUserId) && !string.IsNullOrWhiteSpace(CurrentTenantSchema))
        {
            await ticketingClient.CreateAlertAsync(
                tenantSchema: CurrentTenantSchema!,
                source: "HseRams",
                severity: dto.Status == RamsStatus.Rejected ? "Critical" : "Info",
                title: $"RAMS \"{record.Title}\" v{record.Version} — {dto.Status}",
                message: string.IsNullOrWhiteSpace(dto.IssueNotes)
                    ? $"Your RAMS submission \"{record.Title}\" (v{record.Version}) was marked {dto.Status}."
                    : $"Your RAMS submission \"{record.Title}\" (v{record.Version}) was marked {dto.Status}: {dto.IssueNotes}",
                assignedToUserId: record.UploadedByUserId,
                requiredPermission: "hse.read");
        }

        return Ok(ApiResponse<RamsReadDto>.Ok(ToReadDto(record), "RAMS status updated."));
    }

    private static RamsReadDto ToReadDto(Rams r) => new()
    {
        Id = r.Id,
        SiteId = r.SiteId,
        SiteName = r.SiteName,
        SubcontractorId = r.SubcontractorId,
        SubcontractorName = r.SubcontractorName,
        Title = r.Title,
        Version = r.Version,
        Status = r.Status,
        FileUrl = r.FileUrl,
        IssueNotes = r.IssueNotes,
        ReviewedAt = r.ReviewedAt,
        CreatedAt = r.CreatedAt,
    };
}
