using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OperationsService.Core.DTOs.Assignments;
using OperationsService.Core.DTOs.CheckIns;
using OperationsService.Core.DTOs.Common;
using OperationsService.Core.DTOs.DailySummaries;
using OperationsService.Core.DTOs.Photos;
using OperationsService.Core.Enums;
using OperationsService.Core.Interfaces.Services;
using System.Security.Claims;

namespace OperationsService.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/assignments")]
[Authorize]
public class AssignmentsController(
    IAssignmentService assignmentService,
    ITicketingServiceClient ticketingServiceClient) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
    private string UserName => User.FindFirstValue(ClaimTypes.Name) ?? User.FindFirstValue("name") ?? string.Empty;
    private string DepartmentId => User.FindFirstValue("department_id") ?? string.Empty;
    private List<string> DepartmentIds => (User.FindFirstValue("department_ids") ?? string.Empty)
        .Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
    private bool IsAdmin => User.HasClaim("permission", "system.admin");
    private bool CanReadAll => IsAdmin || User.HasClaim("permission", "operations.read.all");
    private bool CanReadDept => CanReadAll || User.HasClaim("permission", "operations.read.dept")
                                           || User.HasClaim("permission", "operations.write")
                                           || User.HasClaim("permission", "operations.approve");

    private string? BearerToken => Request.Headers.Authorization
        .FirstOrDefault()?.Replace("Bearer ", "", StringComparison.OrdinalIgnoreCase);

    [HttpGet]
    [Authorize(Policy = "Permission:operations.read.own")]
    public async Task<ActionResult<ApiResponse<PaginatedResult<AssignmentReadDto>>>> GetAll([FromQuery] AssignmentFilterParameters filters)
    {
        if (CanReadAll)
        {
            // no scope restriction
        }
        else if (CanReadDept)
        {
            var ids = DepartmentIds;
            if (ids.Count > 0)
                filters.DepartmentIds = ids;
            else
                filters.DepartmentId = DepartmentId;
        }
        else
        {
            // operations.read.own only — filter to assignments this user is a technician on
            filters.TechnicianId = UserId;
        }

        var result = await assignmentService.GetAllAsync(filters, null);
        return Ok(new ApiResponse<PaginatedResult<AssignmentReadDto>> { Success = true, Data = result });
    }

    // Minimal-field picker for other modules' "which assignment is this for" forms — a Fleet
    // Manager filling in a manual dispatch request on behalf of an external driver (who has no
    // account and so can't submit it themselves) holds no operations.* permission at all and
    // can't see GetAll above. Gated on fleet.dispatch.request specifically (the same permission
    // that already gates creating the dispatch itself) rather than widening their real
    // operations-module access just to support this one picker.
    [HttpGet("search-lite")]
    [Authorize(Policy = "Permission:fleet.dispatch.request")]
    public async Task<ActionResult<ApiResponse<PaginatedResult<AssignmentReadDto>>>> SearchLite([FromQuery] string? search, [FromQuery] int pageSize = 20)
    {
        var filters = new AssignmentFilterParameters { Search = search, PageSize = pageSize };
        var result = await assignmentService.GetAllAsync(filters, null);
        var lite = new PaginatedResult<AssignmentReadDto>
        {
            Items = result.Items.Select(a => new AssignmentReadDto { Id = a.Id, Title = a.Title, LocationName = a.LocationName, Status = a.Status }),
            TotalCount = result.TotalCount,
            Page = result.Page,
            PageSize = result.PageSize,
        };
        return Ok(new ApiResponse<PaginatedResult<AssignmentReadDto>> { Success = true, Data = lite });
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<AssignmentReadDto>>> GetById(Guid id)
    {
        var assignment = await assignmentService.GetByIdAsync(id.ToString());
        if (assignment is null) return NotFound(new ApiResponse<AssignmentReadDto> { Success = false, Message = "Assignment not found." });
        return Ok(new ApiResponse<AssignmentReadDto> { Success = true, Data = assignment });
    }

    [HttpPost]
    [Authorize(Policy = "Permission:operations.write")]
    public async Task<ActionResult<ApiResponse<AssignmentReadDto>>> Create([FromBody] CreateAssignmentDto dto)
    {
        if (string.IsNullOrEmpty(dto.DepartmentId)) dto.DepartmentId = DepartmentId;
        var assignment = await assignmentService.CreateAsync(dto, UserId);
        return CreatedAtAction(nameof(GetById), new { id = assignment.Id }, new ApiResponse<AssignmentReadDto> { Success = true, Data = assignment });
    }

    // Technicians and staff can create standalone assignments without full operations.write
    [HttpPost("standalone")]
    [Authorize(Policy = "Permission:operations.read.own")]
    public async Task<ActionResult<ApiResponse<AssignmentReadDto>>> CreateStandalone([FromBody] CreateAssignmentDto dto)
    {
        dto.SourceType = AssignmentSourceType.Standalone;
        if (string.IsNullOrEmpty(dto.DepartmentId)) dto.DepartmentId = DepartmentId;
        if (dto.TechnicianIds.Count == 0)
        {
            dto.TechnicianIds.Add(UserId);
            dto.TechnicianNames.Add(UserName);
        }
        var assignment = await assignmentService.CreateAsync(dto, UserId);
        // Auto-accept so the technician can start work immediately
        assignment = await assignmentService.AcceptAsync(assignment.Id, UserId);
        return CreatedAtAction(nameof(GetById), new { id = assignment.Id }, new ApiResponse<AssignmentReadDto> { Success = true, Data = assignment });
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "Permission:operations.write")]
    public async Task<ActionResult<ApiResponse<AssignmentReadDto>>> Update(Guid id, [FromBody] UpdateAssignmentDto dto)
    {
        var assignment = await assignmentService.UpdateAsync(id.ToString(), dto, UserId);
        return Ok(new ApiResponse<AssignmentReadDto> { Success = true, Data = assignment });
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "Permission:operations.delete")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(Guid id)
    {
        await assignmentService.DeleteAsync(id.ToString(), UserId);
        return Ok(new ApiResponse<object> { Success = true, Message = "Assignment deleted." });
    }

    [HttpPost("{id:guid}/accept")]
    public async Task<ActionResult<ApiResponse<AssignmentReadDto>>> Accept(Guid id)
    {
        var assignment = await assignmentService.AcceptAsync(id.ToString(), UserId);
        if (!string.IsNullOrEmpty(assignment.LinkedTicketId))
            _ = ticketingServiceClient.NotifyWorkUpdateAsync(assignment.LinkedTicketId, assignment.Id, "WorkStarted", null, BearerToken);
        return Ok(new ApiResponse<AssignmentReadDto> { Success = true, Data = assignment });
    }

    [HttpPost("{id:guid}/start")]
    public async Task<ActionResult<ApiResponse<AssignmentReadDto>>> Start(Guid id)
    {
        var assignment = await assignmentService.StartAsync(id.ToString(), UserId);
        if (!string.IsNullOrEmpty(assignment.LinkedTicketId))
            _ = ticketingServiceClient.NotifyWorkUpdateAsync(assignment.LinkedTicketId, assignment.Id, "WorkStarted", null, BearerToken);
        return Ok(new ApiResponse<AssignmentReadDto> { Success = true, Data = assignment });
    }

    [HttpPost("{id:guid}/complete")]
    public async Task<ActionResult<ApiResponse<AssignmentReadDto>>> Complete(Guid id)
    {
        var assignment = await assignmentService.CompleteAsync(id.ToString(), UserId);
        if (!string.IsNullOrEmpty(assignment.LinkedTicketId))
            _ = ticketingServiceClient.NotifyWorkUpdateAsync(assignment.LinkedTicketId, assignment.Id, "WorkCompleted", "Assignment completed by technician", BearerToken);
        return Ok(new ApiResponse<AssignmentReadDto> { Success = true, Data = assignment });
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<ApiResponse<AssignmentReadDto>>> Cancel(Guid id, [FromBody] CancelAssignmentDto dto)
    {
        var assignment = await assignmentService.CancelAsync(id.ToString(), dto, UserId);
        if (!string.IsNullOrEmpty(assignment.LinkedTicketId))
            _ = ticketingServiceClient.NotifyWorkUpdateAsync(assignment.LinkedTicketId, assignment.Id, "WorkCancelled", dto.Reason, BearerToken);
        return Ok(new ApiResponse<AssignmentReadDto> { Success = true, Data = assignment });
    }

    [HttpPost("{id:guid}/submit-for-linking")]
    public async Task<ActionResult<ApiResponse<AssignmentReadDto>>> SubmitForLinking(Guid id)
    {
        var assignment = await assignmentService.SubmitForLinkingAsync(id.ToString(), UserId);
        return Ok(new ApiResponse<AssignmentReadDto> { Success = true, Data = assignment });
    }

    [HttpPost("{id:guid}/link-to-project")]
    [Authorize(Policy = "Permission:operations.approve")]
    public async Task<ActionResult<ApiResponse<AssignmentReadDto>>> LinkToProject(Guid id, [FromBody] LinkToProjectDto dto)
    {
        var assignment = await assignmentService.LinkToProjectAsync(id.ToString(), dto, UserId, UserName);
        return Ok(new ApiResponse<AssignmentReadDto> { Success = true, Data = assignment });
    }

    [HttpPost("{id:guid}/archive")]
    [Authorize(Policy = "Permission:operations.approve")]
    public async Task<ActionResult<ApiResponse<AssignmentReadDto>>> Archive(Guid id, [FromBody] ArchiveStandaloneDto dto)
    {
        var assignment = await assignmentService.ArchiveStandaloneAsync(id.ToString(), dto, UserId);
        return Ok(new ApiResponse<AssignmentReadDto> { Success = true, Data = assignment });
    }

    [HttpGet("standalone/pending")]
    [Authorize(Policy = "Permission:operations.approve")]
    public async Task<ActionResult<ApiResponse<PaginatedResult<AssignmentReadDto>>>> GetPendingLinkage([FromQuery] AssignmentFilterParameters filters)
    {
        filters.AwaitingLinkOnly = true;
        if (!CanReadAll)
        {
            var ids = DepartmentIds;
            if (ids.Count > 0) filters.DepartmentIds = ids;
            else filters.DepartmentId = DepartmentId;
        }
        var result = await assignmentService.GetAllAsync(filters, null);
        return Ok(new ApiResponse<PaginatedResult<AssignmentReadDto>> { Success = true, Data = result });
    }

    // ── Check-ins ──────────────────────────────────────────────────────────────

    [HttpPost("{id:guid}/checkins")]
    public async Task<ActionResult<ApiResponse<CheckInReadDto>>> CheckIn(Guid id, [FromBody] CheckInDto dto)
    {
        dto.AssignmentId = id.ToString();
        var checkIn = await assignmentService.CheckInAsync(dto, UserId, UserName);
        return Ok(new ApiResponse<CheckInReadDto> { Success = true, Data = checkIn });
    }

    [HttpPost("{id:guid}/checkins/{checkInId:guid}/checkout")]
    public async Task<ActionResult<ApiResponse<CheckInReadDto>>> CheckOut(Guid id, Guid checkInId, [FromBody] CheckOutDto dto)
    {
        dto.CheckInId = checkInId.ToString();
        var checkIn = await assignmentService.CheckOutAsync(dto, UserId, UserName);
        return Ok(new ApiResponse<CheckInReadDto> { Success = true, Data = checkIn });
    }

    [HttpGet("{id:guid}/checkins")]
    public async Task<ActionResult<ApiResponse<IEnumerable<CheckInReadDto>>>> GetCheckIns(Guid id)
    {
        var checkIns = await assignmentService.GetCheckInsAsync(id.ToString());
        return Ok(new ApiResponse<IEnumerable<CheckInReadDto>> { Success = true, Data = checkIns });
    }

    // ── Photos ─────────────────────────────────────────────────────────────────

    [HttpPost("{id:guid}/photos")]
    public async Task<ActionResult<ApiResponse<PhotoReadDto>>> UploadPhoto(Guid id, [FromForm] UploadPhotoDto dto, IFormFile file)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new ApiResponse<PhotoReadDto> { Success = false, Message = "No file provided." });

        var uploadsPath = Environment.GetEnvironmentVariable("UPLOADS_PATH")
                         ?? Path.Combine(Directory.GetCurrentDirectory(), "uploads");
        var fileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
        var filePath = Path.Combine(uploadsPath, "photos", fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

        await using (var stream = new FileStream(filePath, FileMode.Create))
            await file.CopyToAsync(stream);

        dto.AssignmentId = id.ToString();
        var photo = await assignmentService.UploadPhotoAsync(dto, $"/uploads/photos/{fileName}", UserId, UserName);
        return Ok(new ApiResponse<PhotoReadDto> { Success = true, Data = photo });
    }

    [HttpGet("{id:guid}/photos")]
    public async Task<ActionResult<ApiResponse<IEnumerable<PhotoReadDto>>>> GetPhotos(Guid id)
    {
        var photos = await assignmentService.GetPhotosAsync(id.ToString());
        return Ok(new ApiResponse<IEnumerable<PhotoReadDto>> { Success = true, Data = photos });
    }

    [HttpDelete("{id:guid}/photos/{photoId:guid}")]
    public async Task<ActionResult<ApiResponse<object>>> DeletePhoto(Guid id, Guid photoId)
    {
        await assignmentService.DeletePhotoAsync(photoId.ToString(), UserId);
        return Ok(new ApiResponse<object> { Success = true, Message = "Photo deleted." });
    }

    // ── Daily summaries ────────────────────────────────────────────────────────

    [HttpPost("{id:guid}/summaries")]
    public async Task<ActionResult<ApiResponse<DailySummaryReadDto>>> CreateSummary(Guid id, [FromBody] CreateDailySummaryDto dto)
    {
        dto.AssignmentId = id.ToString();
        var summary = await assignmentService.CreateDailySummaryAsync(dto, UserId, UserName);
        return Ok(new ApiResponse<DailySummaryReadDto> { Success = true, Data = summary });
    }

    [HttpPut("{id:guid}/summaries/{summaryId:guid}")]
    public async Task<ActionResult<ApiResponse<DailySummaryReadDto>>> UpdateSummary(Guid id, Guid summaryId, [FromBody] UpdateDailySummaryDto dto)
    {
        var summary = await assignmentService.UpdateDailySummaryAsync(summaryId.ToString(), dto, UserId);
        return Ok(new ApiResponse<DailySummaryReadDto> { Success = true, Data = summary });
    }

    [HttpGet("{id:guid}/summaries")]
    public async Task<ActionResult<ApiResponse<IEnumerable<DailySummaryReadDto>>>> GetSummaries(Guid id)
    {
        var summaries = await assignmentService.GetDailySummariesAsync(id.ToString());
        return Ok(new ApiResponse<IEnumerable<DailySummaryReadDto>> { Success = true, Data = summaries });
    }
}
