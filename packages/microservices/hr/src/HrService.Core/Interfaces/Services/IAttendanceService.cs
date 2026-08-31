using HrService.Core.DTOs.Attendance;

namespace HrService.Core.Interfaces.Services;

/// <summary>H4 (ATT-001..008, P27 + P28) — daily clock-in/out with GPS, absence detection and reconciliation
/// against approved leave, the monthly departmental report and the annual attendance scorecard.</summary>
public interface IAttendanceService
{
    Task<AttendanceSummaryDto> GetSummaryAsync();

    // ── Clock-in / clock-out (P27 steps 27.1–27.4) ──
    Task<List<AttendanceRecordDto>> ListRecordsAsync(DateTime? from, DateTime? to, string? employeeId, string? status);
    Task<AttendanceActionResult> ClockInAsync(ClockInDto dto, string? tenantSchema, string userId, string? userName);
    Task<AttendanceActionResult> ClockOutAsync(ClockOutDto dto, string userId);

    // ── Absences (P27 step 27.5 / ATT-004) ──
    Task<List<AbsenceRecordDto>> ListAbsencesAsync(DateTime? from, DateTime? to, string? employeeId, bool? authorised);
    Task<AttendanceActionResult> RecordAbsenceAsync(RecordAbsenceDto dto, string userId);
    /// <summary>Authorises an absence after the fact, which drops its unpaid days.</summary>
    Task<AttendanceActionResult> ExcuseAbsenceAsync(string absenceId, ExcuseAbsenceDto dto, string userId);
    /// <summary>Unpaid days per employee awaiting payroll (H6). Days only — H4 holds no salary data.</summary>
    Task<List<UnpaidAbsenceDto>> ListUnpaidAbsencesAsync(int? year, int? month);

    // ── Working-time configuration (ATT-001) + holidays (the H3-DEC-3 debt) ──
    Task<AttendanceSettingDto> GetSettingsAsync();
    Task<AttendanceActionResult> UpdateSettingsAsync(SaveAttendanceSettingDto dto, string userId);
    Task<List<PublicHolidayDto>> ListHolidaysAsync(int? year, bool includeInactive);
    Task<AttendanceActionResult> CreateHolidayAsync(SavePublicHolidayDto dto, string userId);
    Task<AttendanceActionResult> UpdateHolidayAsync(string id, SavePublicHolidayDto dto, string userId);
    Task<AttendanceActionResult> DeleteHolidayAsync(string id, string userId);
    /// <summary>Installs the Kenyan gazetted fixed-date holidays. Idempotent per date and name.</summary>
    Task<AttendanceActionResult> SeedKenyanHolidaysAsync(int year, string userId);

    // ── Reporting (P28 / ATT-006, ATT-008) ──
    Task<List<AttendanceScorecardDto>> ListScorecardsAsync(int? year, string? employeeId, string? departmentId);
    Task<List<AttendanceMonthlyReportDto>> ListMonthlyReportsAsync(int? year, int? month, string? departmentId);

    /// <summary>The daily sweep: closes open days, detects absences within the backfill window, reconciles them
    /// against approved leave, raises pattern alerts, and generates the prior month's reports and scorecards
    /// once the month has turned. Idempotent throughout.</summary>
    Task<AttendanceSweepResultDto> RunAttendanceSweepAsync(string? tenantSchema, string userId);
}
