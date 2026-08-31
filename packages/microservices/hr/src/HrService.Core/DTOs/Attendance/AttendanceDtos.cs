namespace HrService.Core.DTOs.Attendance;

// ── Clock-in / clock-out (P27) ──
public class ClockInDto
{
    public string EmployeeId { get; set; } = string.Empty;
    /// <summary>Desktop | Biometric | Mobile | Manual. Defaults to the employee's work mode.</summary>
    public string? Method { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    /// <summary>Back-dated entry by HR — the working day this belongs to. Defaults to today.</summary>
    public DateTime? Date { get; set; }
    /// <summary>Explicit clock-in time, for an HR correction. Defaults to now.</summary>
    public DateTime? At { get; set; }
    public string? Notes { get; set; }
}

public class ClockOutDto
{
    public string EmployeeId { get; set; } = string.Empty;
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public DateTime? Date { get; set; }
    public DateTime? At { get; set; }
    public string? Notes { get; set; }
}

public class AttendanceRecordDto
{
    public string Id { get; set; } = string.Empty;
    public string EmployeeId { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }
    public string? EmployeeName { get; set; }
    public string? DepartmentName { get; set; }
    public DateTime Date { get; set; }
    public DateTime? ClockInAt { get; set; }
    public DateTime? ClockOutAt { get; set; }
    public decimal? ClockInLatitude { get; set; }
    public decimal? ClockInLongitude { get; set; }
    public decimal? ClockOutLatitude { get; set; }
    public decimal? ClockOutLongitude { get; set; }
    public string? ClockInMethod { get; set; }
    public string WorkMode { get; set; } = string.Empty;
    public int LateMinutes { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal? HoursWorked { get; set; }
    public string? RecordedBy { get; set; }
    public string? Notes { get; set; }
    public bool HasGps { get; set; }
    /// <summary>Clocked in but never clocked out — an open day.</summary>
    public bool MissingClockOut { get; set; }
}

// ── Absences (P27 step 27.5 / ATT-004) ──
public class AbsenceRecordDto
{
    public string Id { get; set; } = string.Empty;
    public string EmployeeId { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }
    public string? EmployeeName { get; set; }
    public DateTime Date { get; set; }
    public string? Reason { get; set; }
    public bool IsAuthorised { get; set; }
    public string? LinkedLeaveRequestId { get; set; }
    public string? LinkedLeaveTypeCode { get; set; }
    public string Source { get; set; } = string.Empty;
    public decimal UnpaidDays { get; set; }
    public DateTime? ReleasedToPayrollAt { get; set; }
    public DateTime? PatternAlertSentAt { get; set; }
    public string? ExcusedBy { get; set; }
    public DateTime? ExcusedAt { get; set; }
    public string? ExcuseNotes { get; set; }
}

public class RecordAbsenceDto
{
    public string EmployeeId { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string? Reason { get; set; }
    /// <summary>Authorised absences carry no unpaid days.</summary>
    public bool IsAuthorised { get; set; }
}

public class ExcuseAbsenceDto
{
    public string Reason { get; set; } = string.Empty;
}

// ── Settings + holidays (ATT-001, H3-DEC-3 debt) ──
public class AttendanceSettingDto
{
    public string Id { get; set; } = string.Empty;
    public int WorkDayStartMinutes { get; set; }
    public int WorkDayEndMinutes { get; set; }
    public int LunchStartMinutes { get; set; }
    public int LunchMinutes { get; set; }
    public int GraceMinutes { get; set; }
    public int AbsenceCutoffMinutes { get; set; }
    public bool WorksMonday { get; set; }
    public bool WorksTuesday { get; set; }
    public bool WorksWednesday { get; set; }
    public bool WorksThursday { get; set; }
    public bool WorksFriday { get; set; }
    public bool WorksSaturday { get; set; }
    public bool WorksSunday { get; set; }
    public bool RequireGpsForField { get; set; }
    public int AbsencePatternThreshold { get; set; }
    public int AbsencePatternWindowDays { get; set; }
    public int AbsenceBackfillDays { get; set; }
    /// <summary>"08:00" — the start time rendered for people.</summary>
    public string WorkDayStart { get; set; } = string.Empty;
    public string WorkDayEnd { get; set; } = string.Empty;
    public string LateAfter { get; set; } = string.Empty;
    public string AbsentAfter { get; set; } = string.Empty;
}

public class SaveAttendanceSettingDto
{
    public int? WorkDayStartMinutes { get; set; }
    public int? WorkDayEndMinutes { get; set; }
    public int? LunchStartMinutes { get; set; }
    public int? LunchMinutes { get; set; }
    public int? GraceMinutes { get; set; }
    public int? AbsenceCutoffMinutes { get; set; }
    public bool? WorksMonday { get; set; }
    public bool? WorksTuesday { get; set; }
    public bool? WorksWednesday { get; set; }
    public bool? WorksThursday { get; set; }
    public bool? WorksFriday { get; set; }
    public bool? WorksSaturday { get; set; }
    public bool? WorksSunday { get; set; }
    public bool? RequireGpsForField { get; set; }
    public int? AbsencePatternThreshold { get; set; }
    public int? AbsencePatternWindowDays { get; set; }
    public int? AbsenceBackfillDays { get; set; }
}

public class PublicHolidayDto
{
    public string Id { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsRecurring { get; set; }
    public bool IsActive { get; set; }
    public string? Notes { get; set; }
}

public class SavePublicHolidayDto
{
    public DateTime Date { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsRecurring { get; set; }
    public bool? IsActive { get; set; }
    public string? Notes { get; set; }
}

// ── Scorecards + monthly reports (P28) ──
public class AttendanceScorecardDto
{
    public string Id { get; set; } = string.Empty;
    public string EmployeeId { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }
    public string? EmployeeName { get; set; }
    public string? DepartmentName { get; set; }
    public int Year { get; set; }
    public int ExpectedDays { get; set; }
    public int DaysPresent { get; set; }
    public int DaysLate { get; set; }
    public int DaysOnLeave { get; set; }
    public int AuthorisedAbsences { get; set; }
    public int UnauthorisedAbsences { get; set; }
    public int TotalLateMinutes { get; set; }
    public decimal PunctualityRate { get; set; }
    public decimal AbsenceRate { get; set; }
    public decimal OvertimeHours { get; set; }
    public decimal Score { get; set; }
    public DateTime ComputedAt { get; set; }
}

public class AttendanceMonthlyReportDto
{
    public string Id { get; set; } = string.Empty;
    public string? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public string Period { get; set; } = string.Empty;
    public int Headcount { get; set; }
    public int ExpectedDays { get; set; }
    public int DaysPresent { get; set; }
    public int DaysLate { get; set; }
    public int DaysOnLeave { get; set; }
    public int AuthorisedAbsences { get; set; }
    public int UnauthorisedAbsences { get; set; }
    public int TotalLateMinutes { get; set; }
    public decimal UnpaidDays { get; set; }
    public decimal OvertimeHours { get; set; }
    public decimal PunctualityRate { get; set; }
    public decimal AbsenceRate { get; set; }
    public DateTime GeneratedAt { get; set; }
}

/// <summary>What payroll (H6) needs to take unpaid-absence deductions: days, not money. H4 has no salary data.</summary>
public class UnpaidAbsenceDto
{
    public string EmployeeId { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }
    public string? EmployeeName { get; set; }
    public decimal UnpaidDays { get; set; }
    public int AbsenceCount { get; set; }
    public List<DateTime> Dates { get; set; } = [];
}

// ── Summary / results ──
public class AttendanceSummaryDto
{
    public int ExpectedToday { get; set; }
    public int ClockedInToday { get; set; }
    public int LateToday { get; set; }
    public int AbsentToday { get; set; }
    public int OnLeaveToday { get; set; }
    public int MissingClockOutToday { get; set; }
    public bool TodayIsWorkingDay { get; set; }
    public string? TodayNote { get; set; }

    public int UnauthorisedAbsencesThisMonth { get; set; }
    public decimal UnpaidDaysPendingPayroll { get; set; }
    public int AbsencePatternsFlagged { get; set; }
    public int HolidaysConfigured { get; set; }
    public decimal AveragePunctualityThisYear { get; set; }
}

public record AttendanceActionResult(string Status, string Message, string? Id = null);

/// <summary>What the attendance half of the daily sweep did.</summary>
public class AttendanceSweepResultDto
{
    public int AbsencesDetected { get; set; }
    public int AbsencesReconciledToLeave { get; set; }
    public int PatternAlertsRaised { get; set; }
    public int DaysClosedWithoutClockOut { get; set; }
    public int MonthlyReportsGenerated { get; set; }
    public int ScorecardsUpdated { get; set; }
    public string Message { get; set; } = string.Empty;
}
