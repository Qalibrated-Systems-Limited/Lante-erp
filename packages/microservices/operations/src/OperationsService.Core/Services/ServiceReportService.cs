using AutoMapper;
using Microsoft.EntityFrameworkCore;
using OperationsService.Core.DTOs.ServiceReports;
using OperationsService.Core.Entities;
using OperationsService.Core.Enums;
using OperationsService.Core.Interfaces.Repositories;
using OperationsService.Core.Interfaces.Services;

namespace OperationsService.Core.Services;

public class ServiceReportService : IServiceReportService
{
    private readonly IGenericRepository<ServiceReport> _reports;
    private readonly IGenericRepository<FsrEquipment> _equipment;
    private readonly IGenericRepository<Assignment> _assignments;
    private readonly ITimesheetService _timesheets;
    private readonly IMapper _mapper;

    public ServiceReportService(
        IGenericRepository<ServiceReport> reports,
        IGenericRepository<FsrEquipment> equipment,
        IGenericRepository<Assignment> assignments,
        ITimesheetService timesheets,
        IMapper mapper)
    {
        _reports = reports;
        _equipment = equipment;
        _assignments = assignments;
        _timesheets = timesheets;
        _mapper = mapper;
    }

    public async Task<ServiceReportReadDto?> GetByIdAsync(string id)
    {
        var report = await _reports.Query()
            .Include(r => r.Equipment)
            .FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted);
        return report is null ? null : _mapper.Map<ServiceReportReadDto>(report);
    }

    public async Task<ServiceReportReadDto?> GetByAssignmentIdAsync(string assignmentId)
    {
        var report = await _reports.Query()
            .Include(r => r.Equipment)
            .FirstOrDefaultAsync(r => r.AssignmentId == assignmentId && !r.IsDeleted);
        return report is null ? null : _mapper.Map<ServiceReportReadDto>(report);
    }

    private FsrEquipment BuildEquipment(FsrEquipmentDto e, string reportId, string userId) => new()
    {
        ServiceReportId            = reportId,
        ServiceRequestInstrumentId = e.ServiceRequestInstrumentId,
        SerialNumber    = e.SerialNumber,
        Manufacturer    = e.Manufacturer,
        Model           = e.Model,
        TagNumber       = e.TagNumber,
        Description     = e.Description,
        ConditionBefore = e.ConditionBefore,
        ConditionAfter  = e.ConditionAfter,
        WorkDone        = e.WorkDone,
        Notes           = e.Notes,
        CreatedBy       = userId,
        UpdatedBy       = userId,
    };

    public async Task<ServiceReportReadDto> CreateAsync(CreateServiceReportDto dto, string technicianId, string technicianName)
    {
        var report = new ServiceReport
        {
            AssignmentId = dto.AssignmentId,
            TechnicianId = technicianId,
            TechnicianName = technicianName,
            DepartmentType = dto.DepartmentType,
            Status = ServiceReportStatus.Draft,
            CustomerName = dto.CustomerName,
            LocationName = dto.LocationName,
            LocationAddress = dto.LocationAddress,
            ContactPerson = dto.ContactPerson,
            ContactPhone = dto.ContactPhone,
            ContactEmail = dto.ContactEmail,
            NatureOfVisit = dto.NatureOfVisit,
            StartDay = dto.StartDay,
            EndDay = dto.EndDay,
            TotalMinutes = dto.TotalMinutes,
            CustomerComments = dto.CustomerComments,
            SignatureData = dto.SignatureData,
            DetailsJson = dto.Details,
            WorkSummary = dto.WorkSummary,
            MaterialsUsed = dto.MaterialsUsed,
            Recommendations = dto.Recommendations,
            FollowUpRequired = dto.FollowUpRequired,
            FollowUpNotes = dto.FollowUpNotes,
            ClientRating = dto.ClientRating,
            CreatedBy = technicianId,
            UpdatedBy = technicianId
        };

        if (!string.IsNullOrEmpty(dto.SignatureData))
            report.SignedAt = DateTime.UtcNow;

        var created = await _reports.CreateAsync(report);

        // O5-FSR — persist the serviced-equipment set (per-serial service history)
        foreach (var e in dto.Equipment)
            await _equipment.CreateAsync(BuildEquipment(e, created.Id, technicianId));

        return await GetByIdAsync(created.Id) ?? _mapper.Map<ServiceReportReadDto>(created);
    }

    public async Task<ServiceReportReadDto> UpdateAsync(string id, UpdateServiceReportDto dto, string userId)
    {
        var report = await _reports.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Service report {id} not found.");

        if (dto.CustomerName != null) report.CustomerName = dto.CustomerName;
        if (dto.LocationName != null) report.LocationName = dto.LocationName;
        if (dto.LocationAddress != null) report.LocationAddress = dto.LocationAddress;
        if (dto.ContactPerson != null) report.ContactPerson = dto.ContactPerson;
        if (dto.ContactPhone != null) report.ContactPhone = dto.ContactPhone;
        if (dto.ContactEmail != null) report.ContactEmail = dto.ContactEmail;
        if (dto.NatureOfVisit.HasValue) report.NatureOfVisit = dto.NatureOfVisit.Value;
        if (dto.StartDay.HasValue) report.StartDay = dto.StartDay;
        if (dto.EndDay.HasValue) report.EndDay = dto.EndDay;
        if (dto.TotalMinutes.HasValue) report.TotalMinutes = dto.TotalMinutes;
        if (dto.CustomerComments != null) report.CustomerComments = dto.CustomerComments;
        if (dto.Details != null) report.DetailsJson = dto.Details;
        if (dto.WorkSummary != null) report.WorkSummary = dto.WorkSummary;
        if (dto.MaterialsUsed != null) report.MaterialsUsed = dto.MaterialsUsed;
        if (dto.Recommendations != null) report.Recommendations = dto.Recommendations;
        if (dto.FollowUpRequired.HasValue) report.FollowUpRequired = dto.FollowUpRequired.Value;
        if (dto.FollowUpNotes != null) report.FollowUpNotes = dto.FollowUpNotes;
        if (dto.ClientRating.HasValue) report.ClientRating = dto.ClientRating;

        report.UpdatedBy = userId;
        report.UpdatedAt = DateTime.UtcNow;
        await _reports.UpdateAsync(report);

        // O5-FSR — replace the equipment set when the caller supplies one (null = leave unchanged).
        if (dto.Equipment != null)
        {
            var existing = await _equipment.Query().Where(e => e.ServiceReportId == report.Id && !e.IsDeleted).ToListAsync();
            foreach (var old in existing)
            {
                old.IsDeleted = true;
                old.UpdatedBy = userId;
                old.UpdatedAt = DateTime.UtcNow;
                await _equipment.UpdateAsync(old);
            }
            foreach (var e in dto.Equipment)
                await _equipment.CreateAsync(BuildEquipment(e, report.Id, userId));
        }

        return await GetByIdAsync(report.Id) ?? _mapper.Map<ServiceReportReadDto>(report);
    }

    public async Task<ServiceReportReadDto> SignAsync(string id, SignServiceReportDto dto, string userId)
    {
        var report = await _reports.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Service report {id} not found.");
        report.CustomerSignatureName = dto.CustomerName;
        report.CustomerSignatureData = dto.CustomerSignatureData;
        report.TechnicianSignatureName = dto.TechnicianName;
        report.TechnicianSignatureData = dto.TechnicianSignatureData;
        report.SignedAt = DateTime.UtcNow;
        report.Status = ServiceReportStatus.Submitted;
        // O5-FSR — mark the submission time (drives the 24-hour overdue check) + capture client rating.
        report.SubmittedAt = DateTime.UtcNow;
        if (dto.ClientRating.HasValue) report.ClientRating = dto.ClientRating;
        report.UpdatedBy = userId;
        report.UpdatedAt = DateTime.UtcNow;
        await _reports.UpdateAsync(report);
        return await GetByIdAsync(report.Id) ?? _mapper.Map<ServiceReportReadDto>(report);
    }

    // O5-FSR — per-serial service history across all reports (the point of FSR_EQUIPMENT).
    public async Task<IEnumerable<FsrEquipmentDto>> GetEquipmentHistoryAsync(string serialNumber)
    {
        var items = await _equipment.Query()
            .Where(e => e.SerialNumber == serialNumber && !e.IsDeleted)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync();
        return _mapper.Map<List<FsrEquipmentDto>>(items);
    }

    public async Task<ServiceReportReadDto> ReviewAsync(string id, ReviewServiceReportDto dto, string reviewerId, string reviewerName)
    {
        var report = await _reports.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Service report {id} not found.");
        report.Status = dto.Approved ? ServiceReportStatus.Approved : ServiceReportStatus.Rejected;
        report.ApprovedBy = dto.Approved ? reviewerName : null;
        report.ApprovedAt = dto.Approved ? DateTime.UtcNow : null;
        report.RejectionReason = !dto.Approved ? dto.Comments : null;
        report.UpdatedBy = reviewerId;
        report.UpdatedAt = DateTime.UtcNow;
        await _reports.UpdateAsync(report);

        // O4 — on approval, auto-import the technician's logged FSR time onto their weekly timesheet
        // (idempotent by report id). Non-fatal to the approval if the timesheet import fails.
        if (dto.Approved)
        {
            var hours = Math.Round((report.TotalMinutes ?? 0) / 60m, 2);
            if (hours > 0m && !string.IsNullOrEmpty(report.TechnicianId))
            {
                var assignment = string.IsNullOrEmpty(report.AssignmentId) ? null : await _assignments.GetByIdAsync(report.AssignmentId);
                try
                {
                    await _timesheets.ImportFromServiceReportAsync(new FsrTimeImport(
                        EmployeeId:   report.TechnicianId,
                        EmployeeName: report.TechnicianName,
                        DepartmentId: assignment?.DepartmentId ?? report.DepartmentType.ToString(),
                        WorkDate:     report.StartDay ?? report.CreatedAt,
                        Hours:        hours,
                        ProjectId:    assignment?.LinkedProjectId,
                        AssignmentId: report.AssignmentId,
                        SourceRef:    report.Id,
                        Description:  $"FSR: {report.CustomerName}"));
                }
                catch (Exception) { /* import is best-effort; approval already committed */ }
            }
        }

        return _mapper.Map<ServiceReportReadDto>(report);
    }
}
