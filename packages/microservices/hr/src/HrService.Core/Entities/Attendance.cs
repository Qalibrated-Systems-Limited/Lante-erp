using HrService.Core.Enums;

namespace HrService.Core.Entities;

/// <summary>
/// H4 — the tenant's public-holiday calendar. Built here because H4 owns working-time configuration, but its
/// first consumer is H3: leave day counts excluded weekends only, so a leave span crossing Christmas cost the
/// employee a day they should have kept (H3-DEC-3 deferred this here deliberately).
/// <para>Two kinds of holiday, which is why both a date and a recurring flag are needed: fixed-date holidays
/// (Christmas, Jamhuri Day) repeat every year and are matched on month and day, while movable ones (Good Friday,
/// the Eids) are declared per year and stored as one-off dates.</para>
/// </summary>
public class PublicHoliday : BaseEntity
{
    public DateTime Date { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>True for fixed-date holidays — matched on month and day, so one row covers every year.
    /// False for movable feasts, which need a row per year.</summary>
    public bool IsRecurring { get; set; }

    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }
}

/// <summary>
/// H4 (ATT-001) — the tenant's working-time rules: 8:00 to 17:00 with lunch at 13:00, a 15-minute grace period,
/// and an absence declared if nobody has clocked in by 09:00.
/// <para>One row per tenant. Per-branch hours were deferred with the rest of the branch-level configuration, so
/// this is deliberately tenant-wide; when branches get their own hours this becomes the default they override.</para>
/// </summary>
public class AttendanceSetting : BaseEntity
{
    /// <summary>Minutes from midnight, so a time-of-day survives serialisation and Postgres round-trips without
    /// dragging a date along with it.</summary>
    public int WorkDayStartMinutes { get; set; } = 8 * 60;
    public int WorkDayEndMinutes { get; set; } = 17 * 60;
    public int LunchStartMinutes { get; set; } = 13 * 60;
    public int LunchMinutes { get; set; } = 60;

    /// <summary>ATT-002 — how late is late. Clocking in within this many minutes of the start is on time.</summary>
    public int GraceMinutes { get; set; } = 15;

    /// <summary>P27 step 27.5 — no clock-in by this time means the day is treated as an absence.</summary>
    public int AbsenceCutoffMinutes { get; set; } = 9 * 60;

    // Which days the tenant works. Kenya's standard week is Monday to Friday.
    public bool WorksMonday { get; set; } = true;
    public bool WorksTuesday { get; set; } = true;
    public bool WorksWednesday { get; set; } = true;
    public bool WorksThursday { get; set; } = true;
    public bool WorksFriday { get; set; } = true;
    public bool WorksSaturday { get; set; }
    public bool WorksSunday { get; set; }

    /// <summary>ATT-005 — refuse a field clock-in that carries no GPS stamp.</summary>
    public bool RequireGpsForField { get; set; } = true;

    /// <summary>ATT-003 — how many unauthorised absences inside <see cref="AbsencePatternWindowDays"/> trip the
    /// pattern alert.</summary>
    public int AbsencePatternThreshold { get; set; } = 3;
    public int AbsencePatternWindowDays { get; set; } = 30;

    /// <summary>
    /// How far back the daily sweep will detect missed days. Bounded on purpose: an unbounded catch-up would
    /// manufacture absences stretching back to every employee's hire date the first time a tenant switches
    /// attendance on, and a service that was down for a fortnight should still recover the fortnight.
    /// </summary>
    public int AbsenceBackfillDays { get; set; } = 30;
}

/// <summary>
/// H4 (P27, DS2 ATTENDANCE_RECORD) — one employee's one day. Unique per employee and date, which is what makes
/// the daily sweep idempotent and stops a double clock-in creating a second day.
/// </summary>
public class AttendanceRecord : BaseEntity
{
    public string EmployeeId { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }
    public string? EmployeeName { get; set; }
    public string? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }

    /// <summary>The working day this record belongs to, at midnight UTC.</summary>
    public DateTime Date { get; set; }

    public DateTime? ClockInAt { get; set; }
    public DateTime? ClockOutAt { get; set; }

    // Decimal degrees per the P27 design note. Nullable because office staff have no GPS to give.
    public decimal? ClockInLatitude { get; set; }
    public decimal? ClockInLongitude { get; set; }
    public decimal? ClockOutLatitude { get; set; }
    public decimal? ClockOutLongitude { get; set; }

    public ClockInMethod? ClockInMethod { get; set; }
    public WorkMode WorkMode { get; set; }

    /// <summary>ATT-002 — minutes past the grace period, 0 when on time. Computed server-side from the
    /// tenant's configured start time; never supplied by the caller.</summary>
    public int LateMinutes { get; set; }

    public AttendanceStatus Status { get; set; } = AttendanceStatus.Present;

    /// <summary>Clock-out minus clock-in, less the unpaid lunch break when the day spans it. Null until they
    /// clock out.</summary>
    public decimal? HoursWorked { get; set; }

    /// <summary>Set when HR entered or amended the record rather than the employee clocking themselves in.</summary>
    public string? RecordedBy { get; set; }
    public string? Notes { get; set; }

    public Employee? Employee { get; set; }
}

/// <summary>
/// H4 (P27 step 27.5 / ATT-004, DS3 ABSENCE_RECORD) — a working day an employee did not attend.
/// <para>A row is written for every unattended working day, then reconciled: covered by approved leave means
/// authorised and linked, anything else is unauthorised and carries unpaid days. Recording the authorised ones
/// too is what makes the monthly reconciliation a complete picture rather than a list of exceptions.</para>
/// <para><b>H4 owns unpaid DAYS, not money.</b> Turning days into a deduction needs the salary and daily-rate
/// tables that arrive with H5/H6, so <see cref="UnpaidDays"/> is the durable fact and
/// <see cref="ReleasedToPayrollAt"/> is how payroll marks what it has already taken.</para>
/// </summary>
public class AbsenceRecord : BaseEntity
{
    public string EmployeeId { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }
    public string? EmployeeName { get; set; }
    public string? DepartmentId { get; set; }

    public DateTime Date { get; set; }
    public string? Reason { get; set; }

    /// <summary>True when approved leave covers the day, or HR has since excused it.</summary>
    public bool IsAuthorised { get; set; }
    /// <summary>The approved leave request that authorises this day, when there is one (ATT-004).</summary>
    public string? LinkedLeaveRequestId { get; set; }
    public string? LinkedLeaveTypeCode { get; set; }

    public AbsenceSource Source { get; set; } = AbsenceSource.Detected;

    /// <summary>Unpaid days this absence contributes to payroll — 0 once authorised.</summary>
    public decimal UnpaidDays { get; set; }
    /// <summary>Stamped by payroll (H6) once the deduction has been taken, so it is never taken twice.</summary>
    public DateTime? ReleasedToPayrollAt { get; set; }
    /// <summary>H6 — which run took the deduction. Recorded rather than inferred from the date, so a recompute
    /// releases exactly the days it claimed instead of every released day that happens to fall in the month.</summary>
    public string? PayrollRunId { get; set; }

    /// <summary>ATT-003 — stamped on the absence that tripped the pattern threshold, so the alert fires once
    /// per pattern rather than on every sweep while the window stays full.</summary>
    public DateTime? PatternAlertSentAt { get; set; }

    public string? ExcusedBy { get; set; }
    public DateTime? ExcusedAt { get; set; }
    public string? ExcuseNotes { get; set; }

    public Employee? Employee { get; set; }
}

/// <summary>
/// H4 (P28 step 28.4 / ATT-008, DS4 ATTENDANCE_SCORECARD) — one employee's cumulative attendance for a year,
/// recomputed rather than incremented so it always agrees with the underlying records.
/// <para>The DFD keys this on a fiscal year; HR has no fiscal-calendar table and the one that exists belongs to
/// finance, so this uses the calendar year and leaves the fiscal alignment to whoever needs it.</para>
/// </summary>
public class AttendanceScorecard : BaseEntity
{
    public string EmployeeId { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }
    public string? EmployeeName { get; set; }
    public string? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }

    public int Year { get; set; }

    /// <summary>Working days the employee was expected, excluding weekends, holidays and days before they were
    /// hired — the denominator for both rates.</summary>
    public int ExpectedDays { get; set; }
    public int DaysPresent { get; set; }
    public int DaysLate { get; set; }
    public int DaysOnLeave { get; set; }
    public int AuthorisedAbsences { get; set; }
    public int UnauthorisedAbsences { get; set; }
    public int TotalLateMinutes { get; set; }

    /// <summary>Attended days that were on time, as a percentage of days attended. 0 when
    /// <see cref="DaysPresent"/> is 0 — punctuality is undefined with no arrivals, so read the two together
    /// rather than reporting a bare 0%.</summary>
    public decimal PunctualityRate { get; set; }
    /// <summary>Unauthorised absences as a percentage of expected days.</summary>
    public decimal AbsenceRate { get; set; }

    /// <summary>Approved overtime in the year. Zero until H6 builds OVERTIME_REQUEST — the column exists so the
    /// scorecard's shape does not change when that arrives.</summary>
    public decimal OvertimeHours { get; set; }

    /// <summary>0–100, feeding the H9 KPI appraisal (ATT-008). Weighted 70 punctuality / 30 attendance; the
    /// formula is stated on the service so H9 can reweight it deliberately rather than by accident.</summary>
    public decimal Score { get; set; }

    public DateTime ComputedAt { get; set; } = DateTime.UtcNow;

    public Employee? Employee { get; set; }
}

/// <summary>
/// H4 (P28 steps 28.1–28.5 / ATT-006) — the monthly departmental report, generated on the 1st for the prior
/// month.
/// <para>Stored rather than merely "distributed" for two reasons: the row's existence per (department, year,
/// month) is what makes the monthly job idempotent, and ATT-006 requires a department head to see their own
/// department's figures, which needs the numbers to still exist when they look.</para>
/// </summary>
public class AttendanceMonthlyReport : BaseEntity
{
    /// <summary>Null for the company-wide roll-up covering staff with no department.</summary>
    public string? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }

    public int Year { get; set; }
    public int Month { get; set; }

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

    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}
