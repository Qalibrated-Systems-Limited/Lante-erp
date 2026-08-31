using OperationsService.Core.DTOs.Common;
using OperationsService.Core.DTOs.Timesheets;

namespace OperationsService.Core.Interfaces.Services;

/// <summary>
/// O4 — weekly timesheets: entry capture, overtime pre-approval (HR seam), line-manager approval that
/// posts labour to Finance (project actuals) + hours to HR (payroll), and FSR time auto-import.
/// </summary>
public interface ITimesheetService
{
    Task<TimesheetReadDto?> GetByIdAsync(string id);
    Task<PaginatedResult<TimesheetReadDto>> GetAllAsync(TimesheetFilterParameters filters);
    Task<TimesheetReadDto> CreateAsync(CreateTimesheetDto dto, string employeeId, string employeeName, string departmentId);

    Task<TimesheetEntryReadDto> AddEntryAsync(string timesheetId, CreateTimesheetEntryDto dto, string userId);
    Task<TimesheetEntryReadDto> UpdateEntryAsync(string entryId, UpdateTimesheetEntryDto dto, string userId);
    Task DeleteEntryAsync(string entryId, string userId);
    Task<TimesheetEntryReadDto> RequestOvertimeAsync(string entryId, RequestOvertimeDto dto, string userId);

    Task<TimesheetReadDto> SubmitAsync(string timesheetId, string userId);
    Task<TimesheetReadDto> ReviewAsync(string timesheetId, ReviewTimesheetDto dto, string approverId, string approverName);

    /// <summary>O4 — auto-import a technician's FSR time onto their weekly timesheet (idempotent by SourceRef).</summary>
    Task ImportFromServiceReportAsync(FsrTimeImport import);

    /// <summary>
    /// PR2 — turn a completed check-in/check-out pair into a timesheet line (idempotent by SourceRef).
    /// Returns the entry id so the check-in can record what it produced, or null if nothing was
    /// captured (no measurable time, or already imported).
    /// </summary>
    Task<string?> ImportFromCheckInAsync(CheckInTimeImport import);
}

public record FsrTimeImport(
    string EmployeeId,
    string EmployeeName,
    string DepartmentId,
    DateTime WorkDate,
    decimal Hours,
    string? ProjectId,
    string? AssignmentId,
    string SourceRef,      // ServiceReport id (dedup key)
    string Description);

/// <param name="Hours">Elapsed time between check-in and check-out, already rounded by the caller.</param>
/// <param name="SourceRef">The check-in id — the dedup key, mirroring how FsrImport uses the report id.</param>
public record CheckInTimeImport(
    string EmployeeId,
    string EmployeeName,
    string DepartmentId,
    DateTime WorkDate,
    decimal Hours,
    string? ProjectId,
    string? AssignmentId,
    string? ProjectTaskId,
    bool IsBillable,
    string SourceRef,
    string Description);
