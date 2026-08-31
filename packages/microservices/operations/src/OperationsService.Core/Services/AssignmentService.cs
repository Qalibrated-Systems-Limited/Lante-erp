using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Npgsql.EntityFrameworkCore.PostgreSQL;
using OperationsService.Core.DTOs.Assignments;
using OperationsService.Core.DTOs.CheckIns;
using OperationsService.Core.DTOs.Common;
using OperationsService.Core.DTOs.DailySummaries;
using OperationsService.Core.DTOs.Photos;
using OperationsService.Core.Entities;
using OperationsService.Core.Enums;
using OperationsService.Core.Interfaces.Repositories;
using OperationsService.Core.Interfaces.Services;

namespace OperationsService.Core.Services;

public class AssignmentService : IAssignmentService
{
    private readonly IGenericRepository<Assignment> _assignments;
    private readonly IGenericRepository<AssignedTechnician> _technicians;
    private readonly IGenericRepository<CheckIn> _checkIns;
    private readonly IGenericRepository<Photo> _photos;
    private readonly IGenericRepository<DailySummary> _dailySummaries;
    private readonly IGenericRepository<ServiceRequest> _serviceRequests;
    private readonly ITimesheetService _timesheets;
    private readonly IMapper _mapper;

    public AssignmentService(
        IGenericRepository<Assignment> assignments,
        IGenericRepository<AssignedTechnician> technicians,
        IGenericRepository<CheckIn> checkIns,
        IGenericRepository<Photo> photos,
        IGenericRepository<DailySummary> dailySummaries,
        IGenericRepository<ServiceRequest> serviceRequests,
        ITimesheetService timesheets,
        IMapper mapper)
    {
        _assignments = assignments;
        _technicians = technicians;
        _checkIns = checkIns;
        _photos = photos;
        _dailySummaries = dailySummaries;
        _serviceRequests = serviceRequests;
        _timesheets = timesheets;
        _mapper = mapper;
    }

    public async Task<AssignmentReadDto?> GetByIdAsync(string id)
    {
        var assignment = await _assignments.Query()
            .AsNoTracking()
            .Include(a => a.Technicians)
            .FirstOrDefaultAsync(a => a.Id == id && !a.IsDeleted);
        return assignment is null ? null : _mapper.Map<AssignmentReadDto>(assignment);
    }

    public async Task<PaginatedResult<AssignmentReadDto>> GetAllAsync(AssignmentFilterParameters filters, string? departmentId)
    {
        // Technicians IS used by AssignmentReadDto (unlike ProjectReadDto's Milestones, which is
        // only counted — see ProjectService.GetAllAsync), so the Include stays; only the
        // change-tracking overhead — pointless for a read-only list response — is removed here.
        var query = _assignments.Query()
            .AsNoTracking()
            .Include(a => a.Technicians)
            .Where(a => !a.IsDeleted);

        if (!string.IsNullOrEmpty(departmentId))
            query = query.Where(a => a.DepartmentId == departmentId);

        if (filters.DepartmentIds is { Count: > 0 })
            query = query.Where(a => string.IsNullOrEmpty(a.DepartmentId) || filters.DepartmentIds.Contains(a.DepartmentId));
        else if (!string.IsNullOrEmpty(filters.DepartmentId))
            query = query.Where(a => a.DepartmentId == filters.DepartmentId || string.IsNullOrEmpty(a.DepartmentId));

        if (!string.IsNullOrEmpty(filters.Search))
            // ILike (not Contains) so Npgsql translates to a form the GIN trigram index on
            // Title can actually serve — Contains()'s LIKE '%term%' has a leading wildcard that
            // no ordinary index, and no plain LIKE either, can use (#279).
            query = query.Where(a => EF.Functions.ILike(a.Title, $"%{filters.Search}%"));

        if (!string.IsNullOrEmpty(filters.TechnicianId))
            query = query.Where(a => a.Technicians.Any(t => t.UserId == filters.TechnicianId));

        if (!string.IsNullOrEmpty(filters.ManagerId))
            query = query.Where(a => a.ManagerId == filters.ManagerId);

        if (filters.Status.HasValue)
            query = query.Where(a => (int)a.Status == filters.Status.Value);

        if (filters.SourceType.HasValue)
            query = query.Where(a => (int)a.SourceType == filters.SourceType.Value);

        if (filters.AwaitingLinkOnly == true)
            query = query.Where(a => a.SourceType == AssignmentSourceType.Standalone
                                  && a.LinkedProjectId == null
                                  && a.Status != AssignmentStatus.Archived);

        query = filters.SortDescending
            ? query.OrderByDescending(a => a.CreatedAt)
            : query.OrderBy(a => a.CreatedAt);

        var total = await query.CountAsync();
        var items = await query.Skip((filters.Page - 1) * filters.PageSize).Take(filters.PageSize).ToListAsync();

        return new PaginatedResult<AssignmentReadDto>
        {
            Items = _mapper.Map<List<AssignmentReadDto>>(items),
            TotalCount = total,
            Page = filters.Page,
            PageSize = filters.PageSize
        };
    }

    public async Task<AssignmentReadDto> CreateAsync(CreateAssignmentDto dto, string managerId)
    {
        var assignment = _mapper.Map<Assignment>(dto);
        assignment.ManagerId = managerId;
        assignment.Status = AssignmentStatus.Pending;
        assignment.CreatedBy = managerId;
        assignment.UpdatedBy = managerId;

        var created = await _assignments.CreateAsync(assignment);

        foreach (var (techId, techName) in dto.TechnicianIds.Zip(dto.TechnicianNames))
        {
            await _technicians.CreateAsync(new AssignedTechnician
            {
                AssignmentId = created.Id,
                UserId = techId,
                UserName = techName,
                AssignedAt = DateTime.UtcNow,
                CreatedBy = managerId,
                UpdatedBy = managerId
            });
        }

        return await GetByIdAsync(created.Id) ?? _mapper.Map<AssignmentReadDto>(created);
    }

    public async Task<AssignmentReadDto> UpdateAsync(string id, UpdateAssignmentDto dto, string userId)
    {
        var assignment = await _assignments.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Assignment {id} not found.");
        _mapper.Map(dto, assignment);
        assignment.UpdatedBy = userId;
        assignment.UpdatedAt = DateTime.UtcNow;
        await _assignments.UpdateAsync(assignment);
        return await GetByIdAsync(id) ?? _mapper.Map<AssignmentReadDto>(assignment);
    }

    public async Task DeleteAsync(string id, string userId)
    {
        var assignment = await _assignments.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Assignment {id} not found.");
        assignment.IsDeleted = true;
        assignment.UpdatedBy = userId;
        assignment.UpdatedAt = DateTime.UtcNow;
        await _assignments.UpdateAsync(assignment);
    }

    public async Task<AssignmentReadDto> AcceptAsync(string id, string technicianId)
    {
        var assignment = await _assignments.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Assignment {id} not found.");
        assignment.Status = AssignmentStatus.Accepted;
        assignment.AcceptedAt = DateTime.UtcNow;
        assignment.UpdatedBy = technicianId;
        assignment.UpdatedAt = DateTime.UtcNow;
        await _assignments.UpdateAsync(assignment);
        return await GetByIdAsync(id) ?? _mapper.Map<AssignmentReadDto>(assignment);
    }

    public async Task<AssignmentReadDto> StartAsync(string id, string technicianId)
    {
        var assignment = await _assignments.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Assignment {id} not found.");
        assignment.Status = AssignmentStatus.InProgress;
        assignment.StartedAt = DateTime.UtcNow;
        assignment.UpdatedBy = technicianId;
        assignment.UpdatedAt = DateTime.UtcNow;
        await _assignments.UpdateAsync(assignment);
        return await GetByIdAsync(id) ?? _mapper.Map<AssignmentReadDto>(assignment);
    }

    public async Task<AssignmentReadDto> CompleteAsync(string id, string technicianId)
    {
        var assignment = await _assignments.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Assignment {id} not found.");
        assignment.Status = AssignmentStatus.Completed;
        assignment.CompletedAt = DateTime.UtcNow;
        assignment.UpdatedBy = technicianId;
        assignment.UpdatedAt = DateTime.UtcNow;
        await _assignments.UpdateAsync(assignment);

        // O5.4 — a field-service SR completes when its assignment does (calibration SRs complete on
        // certificate instead). Replaces the old ticketing InternalWorkUpdate SR-completion (#11)
        // now that operations owns the SR. Direct entity update (no IServiceRequestService — that
        // would be a circular dependency, since it depends on IAssignmentService for dispatch).
        if (!string.IsNullOrEmpty(assignment.ServiceRequestId))
        {
            var sr = await _serviceRequests.GetByIdAsync(assignment.ServiceRequestId);
            if (sr is { IsDeleted: false } &&
                sr.Status is ServiceRequestStatus.InProgress or ServiceRequestStatus.QuotationApproved)
            {
                sr.Status = ServiceRequestStatus.Completed;
                sr.UpdatedBy = technicianId;
                sr.UpdatedAt = DateTime.UtcNow;
                await _serviceRequests.UpdateAsync(sr);
            }
        }

        return await GetByIdAsync(id) ?? _mapper.Map<AssignmentReadDto>(assignment);
    }

    public async Task<AssignmentReadDto> CancelAsync(string id, CancelAssignmentDto dto, string userId)
    {
        var assignment = await _assignments.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Assignment {id} not found.");
        assignment.Status = AssignmentStatus.Cancelled;
        assignment.CancellationReason = dto.Reason;
        assignment.CancelledAt = DateTime.UtcNow;
        assignment.UpdatedBy = userId;
        assignment.UpdatedAt = DateTime.UtcNow;
        await _assignments.UpdateAsync(assignment);
        return await GetByIdAsync(id) ?? _mapper.Map<AssignmentReadDto>(assignment);
    }

    public async Task<AssignmentReadDto> SubmitForLinkingAsync(string id, string userId)
    {
        var assignment = await _assignments.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Assignment {id} not found.");

        if (assignment.SourceType != AssignmentSourceType.Standalone)
            throw new InvalidOperationException("Only standalone assignments can be submitted for project linking.");

        if (assignment.Status != AssignmentStatus.Completed)
            throw new InvalidOperationException("Assignment must be completed before submitting for project linking.");

        assignment.Status = AssignmentStatus.AwaitingProjectLink;
        assignment.UpdatedBy = userId;
        assignment.UpdatedAt = DateTime.UtcNow;
        await _assignments.UpdateAsync(assignment);
        return await GetByIdAsync(id) ?? _mapper.Map<AssignmentReadDto>(assignment);
    }

    public async Task<AssignmentReadDto> LinkToProjectAsync(string id, LinkToProjectDto dto, string managerId, string managerName)
    {
        var assignment = await _assignments.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Assignment {id} not found.");

        if (assignment.Status != AssignmentStatus.AwaitingProjectLink)
            throw new InvalidOperationException("Assignment is not awaiting project linkage.");

        assignment.LinkedProjectId = dto.ProjectId;
        assignment.LinkedMilestoneId = dto.MilestoneId;
        assignment.LinkedAt = DateTime.UtcNow;
        assignment.LinkedBy = managerId;
        assignment.Status = AssignmentStatus.Completed;
        assignment.UpdatedBy = managerId;
        assignment.UpdatedAt = DateTime.UtcNow;
        await _assignments.UpdateAsync(assignment);
        return await GetByIdAsync(id) ?? _mapper.Map<AssignmentReadDto>(assignment);
    }

    public async Task<AssignmentReadDto> ArchiveStandaloneAsync(string id, ArchiveStandaloneDto dto, string userId)
    {
        var assignment = await _assignments.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Assignment {id} not found.");

        if (assignment.SourceType != AssignmentSourceType.Standalone)
            throw new InvalidOperationException("Only standalone assignments can be archived via this action.");

        if (assignment.Status != AssignmentStatus.AwaitingProjectLink)
            throw new InvalidOperationException("Assignment must be awaiting project linkage to be archived.");

        assignment.Status = AssignmentStatus.Archived;
        assignment.ArchiveReason = dto.Reason;
        assignment.UpdatedBy = userId;
        assignment.UpdatedAt = DateTime.UtcNow;
        await _assignments.UpdateAsync(assignment);
        return await GetByIdAsync(id) ?? _mapper.Map<AssignmentReadDto>(assignment);
    }

    // Check-ins
    public async Task<CheckInReadDto> CheckInAsync(CheckInDto dto, string userId, string userName)
    {
        // PR2 — stamp what the time is against at check-in, while the context is known. Explicit
        // values win; otherwise inherit the assignment's project/task links so field staff do not have
        // to restate something the assignment already records.
        var assignment = await _assignments.GetByIdAsync(dto.AssignmentId);

        var checkIn = new CheckIn
        {
            AssignmentId = dto.AssignmentId,
            UserId = userId,
            Latitude = dto.Latitude ?? 0,
            Longitude = dto.Longitude ?? 0,
            CheckInTime = DateTime.UtcNow,
            Status = CheckInStatus.OnSite,
            Notes = dto.Notes,
            ProjectId = dto.ProjectId ?? assignment?.LinkedProjectId,
            ProjectTaskId = dto.ProjectTaskId ?? assignment?.LinkedProjectTaskId,
            IsBillable = dto.IsBillable ?? true,
            CreatedBy = userId,
            UpdatedBy = userId
        };
        var created = await _checkIns.CreateAsync(checkIn);
        var readDto = _mapper.Map<CheckInReadDto>(created);
        readDto.UserName = userName;
        return readDto;
    }

    public async Task<CheckInReadDto> CheckOutAsync(CheckOutDto dto, string userId, string userName)
    {
        var checkIn = await _checkIns.GetByIdAsync(dto.CheckInId)
            ?? throw new KeyNotFoundException("Check-in not found.");
        checkIn.CheckOutTime = DateTime.UtcNow;
        checkIn.Status = CheckInStatus.CheckedOut;
        if (dto.Notes != null) checkIn.Notes = dto.Notes;
        if (dto.IsBillable.HasValue) checkIn.IsBillable = dto.IsBillable.Value;
        checkIn.UpdatedBy = userId;
        checkIn.UpdatedAt = DateTime.UtcNow;
        var updated = await _checkIns.UpdateAsync(checkIn);

        // PR2 — the check-out is the moment the interval is known, so capture the time now rather than
        // asking the technician to retype it. Best-effort: a timesheet problem must not fail the
        // check-out, which is the field app's primary action and may be on a poor connection.
        try
        {
            var entryId = await CaptureCheckInTimeAsync(updated, userName);
            if (entryId is not null && updated.TimesheetEntryId != entryId)
            {
                updated.TimesheetEntryId = entryId;
                await _checkIns.UpdateAsync(updated);
            }
        }
        catch (Exception) { /* time capture is best-effort; the check-out itself stands */ }

        var readDto = _mapper.Map<CheckInReadDto>(updated);
        readDto.CheckOutLatitude = dto.Latitude;
        readDto.CheckOutLongitude = dto.Longitude;
        return readDto;
    }

    /// <summary>
    /// Derives hours from the check-in/check-out pair and hands them to the timesheet. Rounds to two
    /// decimals — the clock is accurate to the second, but billing to the second is false precision.
    /// </summary>
    private async Task<string?> CaptureCheckInTimeAsync(CheckIn checkIn, string userName)
    {
        if (checkIn.CheckOutTime is null) return null;

        var elapsed = checkIn.CheckOutTime.Value - checkIn.CheckInTime;
        if (elapsed <= TimeSpan.Zero) return null;

        var hours = decimal.Round((decimal)elapsed.TotalHours, 2);
        if (hours <= 0m) return null;

        var assignment = await _assignments.GetByIdAsync(checkIn.AssignmentId);
        var description = assignment?.Title is { Length: > 0 } title
            ? $"Site time — {title}"
            : "Site time (check-in)";

        return await _timesheets.ImportFromCheckInAsync(new CheckInTimeImport(
            EmployeeId:    checkIn.UserId,
            EmployeeName:  userName,
            DepartmentId:  assignment?.DepartmentId ?? string.Empty,
            // The work belongs to the day it started on: a visit running past midnight is one visit,
            // not two, and splitting it would put half the time on a week nobody worked.
            WorkDate:      checkIn.CheckInTime.Date,
            Hours:         hours,
            ProjectId:     checkIn.ProjectId ?? assignment?.LinkedProjectId,
            AssignmentId:  checkIn.AssignmentId,
            ProjectTaskId: checkIn.ProjectTaskId ?? assignment?.LinkedProjectTaskId,
            IsBillable:    checkIn.IsBillable,
            SourceRef:     checkIn.Id,
            Description:   description));
    }

    public async Task<IEnumerable<CheckInReadDto>> GetCheckInsAsync(string assignmentId)
    {
        var checkIns = await _checkIns.Query()
            .Where(c => c.AssignmentId == assignmentId && !c.IsDeleted)
            .OrderByDescending(c => c.CheckInTime)
            .ToListAsync();
        return _mapper.Map<List<CheckInReadDto>>(checkIns);
    }

    // Photos
    public async Task<PhotoReadDto> UploadPhotoAsync(UploadPhotoDto dto, string fileUrl, string userId, string userName)
    {
        var photo = new Photo
        {
            AssignmentId = dto.AssignmentId,
            UploadedByUserId = userId,
            Type = dto.PhotoType,
            FileName = Path.GetFileName(fileUrl),
            FilePath = fileUrl,
            StorageUrl = fileUrl,
            FileSize = 0,
            ContentType = "image/jpeg",
            Caption = dto.Caption,
            CapturedAt = DateTime.UtcNow,
            CreatedBy = userId,
            UpdatedBy = userId
        };
        var created = await _photos.CreateAsync(photo);
        var readDto = _mapper.Map<PhotoReadDto>(created);
        readDto.UploadedByName = userName;
        return readDto;
    }

    public async Task<IEnumerable<PhotoReadDto>> GetPhotosAsync(string assignmentId)
    {
        var photos = await _photos.Query()
            .Where(p => p.AssignmentId == assignmentId && !p.IsDeleted)
            .OrderByDescending(p => p.CapturedAt)
            .ToListAsync();
        return _mapper.Map<List<PhotoReadDto>>(photos);
    }

    public async Task DeletePhotoAsync(string photoId, string userId)
    {
        var photo = await _photos.GetByIdAsync(photoId)
            ?? throw new KeyNotFoundException("Photo not found.");
        photo.IsDeleted = true;
        photo.UpdatedBy = userId;
        photo.UpdatedAt = DateTime.UtcNow;
        await _photos.UpdateAsync(photo);
    }

    // Daily summaries
    public async Task<DailySummaryReadDto> CreateDailySummaryAsync(CreateDailySummaryDto dto, string userId, string userName)
    {
        var summary = new DailySummary
        {
            AssignmentId = dto.AssignmentId,
            SubmittedByUserId = userId,
            SubmittedByName = userName,
            Date = dto.Date,
            Summary = dto.Summary,
            HoursWorked = dto.HoursWorked,
            Challenges = dto.Challenges,
            NextDayPlan = dto.NextDayPlan,
            ExpensesIncurred = dto.ExpensesIncurred,
            CreatedBy = userId,
            UpdatedBy = userId
        };
        var created = await _dailySummaries.CreateAsync(summary);
        return _mapper.Map<DailySummaryReadDto>(created);
    }

    public async Task<DailySummaryReadDto> UpdateDailySummaryAsync(string summaryId, UpdateDailySummaryDto dto, string userId)
    {
        var summary = await _dailySummaries.GetByIdAsync(summaryId)
            ?? throw new KeyNotFoundException("Daily summary not found.");
        if (dto.Summary != null) summary.Summary = dto.Summary;
        if (dto.HoursWorked.HasValue) summary.HoursWorked = dto.HoursWorked.Value;
        if (dto.Challenges != null) summary.Challenges = dto.Challenges;
        if (dto.NextDayPlan != null) summary.NextDayPlan = dto.NextDayPlan;
        if (dto.ExpensesIncurred.HasValue) summary.ExpensesIncurred = dto.ExpensesIncurred;
        summary.UpdatedBy = userId;
        summary.UpdatedAt = DateTime.UtcNow;
        return _mapper.Map<DailySummaryReadDto>(await _dailySummaries.UpdateAsync(summary));
    }

    public async Task<IEnumerable<DailySummaryReadDto>> GetDailySummariesAsync(string assignmentId)
    {
        var summaries = await _dailySummaries.Query()
            .Where(s => s.AssignmentId == assignmentId && !s.IsDeleted)
            .OrderByDescending(s => s.Date)
            .ToListAsync();
        return _mapper.Map<List<DailySummaryReadDto>>(summaries);
    }
}
