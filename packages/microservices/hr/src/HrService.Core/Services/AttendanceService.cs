using Microsoft.EntityFrameworkCore;
using HrService.Core.DTOs.Attendance;
using HrService.Core.Entities;
using HrService.Core.Enums;
using HrService.Core.Interfaces.Repositories;
using HrService.Core.Interfaces.Services;

namespace HrService.Core.Services;

/// <summary>
/// H4 (P27 + P28) — attendance: clock-in/out with GPS, absence detection, reconciliation against approved leave,
/// and the monthly/annual reporting that feeds payroll and the KPI appraisal.
/// <para><b>Absence is inferred from silence, so it must be reconcilable.</b> The sweep writes a row for every
/// unattended working day and then checks it against approved leave: covered means authorised and linked,
/// uncovered means unauthorised with unpaid days. Recording the authorised days too is what makes ATT-004 a
/// complete daily picture instead of a list of exceptions, and it lets someone see at a glance why a day is
/// blank.</para>
/// <para><b>H4 owns unpaid DAYS, never money.</b> Converting days into a deduction needs the salary and
/// daily-rate tables that arrive with H5/H6; this phase records the day count and payroll stamps
/// <c>ReleasedToPayrollAt</c> when it takes it, so the same absence cannot be deducted twice.</para>
/// <para>Everything the sweep does is keyed on (employee, date) or (department, year, month) uniqueness, so it is
/// safe to run repeatedly and it catches up rather than losing a day the service was down for.</para>
/// </summary>
public class AttendanceService(
    IGenericRepository<Employee> employees,
    IGenericRepository<AttendanceRecord> records,
    IGenericRepository<AbsenceRecord> absences,
    IGenericRepository<AttendanceScorecard> scorecards,
    IGenericRepository<AttendanceMonthlyReport> reports,
    IGenericRepository<AttendanceSetting> settings,
    IGenericRepository<PublicHoliday> holidays,
    IGenericRepository<LeaveRequest> leaveRequests,
    IGenericRepository<HrAuditLog> audit,
    IWorkCalendar calendar,
    IHrAlertGateway notifier) : IAttendanceService
{
    /// <summary>
    /// ATT-008 — the scorecard weighting that feeds the KPI appraisal: punctuality counts for 70, turning up at
    /// all for 30. Stated here as named constants rather than buried in the arithmetic so H9 can reweight it
    /// deliberately instead of by accident.
    /// </summary>
    private const decimal PunctualityWeight = 70m;
    private const decimal AttendanceWeight = 30m;

    // ══ Summary ══
    public async Task<AttendanceSummaryDto> GetSummaryAsync()
    {
        var today = DateTime.UtcNow.Date;
        var config = await calendar.GetSettingsAsync();
        var isWorkingDay = await calendar.IsWorkingDayAsync(today);
        var holidayToday = (await calendar.HolidayDatesAsync(today, today)).Contains(today);

        var todays = await records.Query().AsNoTracking().Where(r => r.Date == today).ToListAsync();
        var expected = await employees.Query().CountAsync(e => e.Status != EmploymentStatus.Resigned
                                                           && e.Status != EmploymentStatus.Terminated
                                                           && e.HireDate <= today);

        var monthStart = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthAbsences = await absences.Query().AsNoTracking().Where(a => a.Date >= monthStart).ToListAsync();
        var yearScorecards = await scorecards.Query().AsNoTracking().Where(s => s.Year == today.Year).ToListAsync();

        return new AttendanceSummaryDto
        {
            ExpectedToday = isWorkingDay ? expected : 0,
            ClockedInToday = todays.Count(r => r.ClockInAt is not null),
            LateToday = todays.Count(r => r.Status == AttendanceStatus.Late),
            AbsentToday = todays.Count(r => r.Status == AttendanceStatus.Absent),
            OnLeaveToday = todays.Count(r => r.Status == AttendanceStatus.OnLeave),
            MissingClockOutToday = todays.Count(r => r.ClockInAt is not null && r.ClockOutAt is null),
            TodayIsWorkingDay = isWorkingDay,
            TodayNote = isWorkingDay
                ? $"Working day — late after {Hhmm(config.WorkDayStartMinutes + config.GraceMinutes)}, absent after {Hhmm(config.AbsenceCutoffMinutes)}."
                : holidayToday ? "Public holiday — nobody is expected." : "Non-working day — nobody is expected.",

            UnauthorisedAbsencesThisMonth = monthAbsences.Count(a => !a.IsAuthorised),
            UnpaidDaysPendingPayroll = (await absences.Query().AsNoTracking()
                .Where(a => !a.IsAuthorised && a.ReleasedToPayrollAt == null).SumAsync(a => (decimal?)a.UnpaidDays)) ?? 0,
            AbsencePatternsFlagged = monthAbsences.Count(a => a.PatternAlertSentAt != null),
            HolidaysConfigured = await holidays.Query().CountAsync(h => h.IsActive),
            AveragePunctualityThisYear = yearScorecards.Count == 0
                ? 0
                : Math.Round(yearScorecards.Average(s => s.PunctualityRate), 1),
        };
    }

    // ══ Clock-in / clock-out (P27) ══
    public async Task<List<AttendanceRecordDto>> ListRecordsAsync(DateTime? from, DateTime? to, string? employeeId, string? status)
    {
        var q = records.Query().AsNoTracking();
        if (from is not null) q = q.Where(r => r.Date >= from.Value.Date);
        if (to is not null) q = q.Where(r => r.Date <= to.Value.Date);
        if (!string.IsNullOrWhiteSpace(employeeId)) q = q.Where(r => r.EmployeeId == employeeId);
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<AttendanceStatus>(status, true, out var st))
            q = q.Where(r => r.Status == st);

        var list = await q.OrderByDescending(r => r.Date).ThenBy(r => r.EmployeeNumber).Take(1000).ToListAsync();
        return list.Select(ToDto).ToList();
    }

    /// <summary>
    /// P27 steps 27.1–27.3. The late calculation is server-side from the tenant's configured start time and grace
    /// period — a client that could state its own lateness would make ATT-002 meaningless.
    /// </summary>
    public async Task<AttendanceActionResult> ClockInAsync(ClockInDto dto, string? tenantSchema, string userId, string? userName)
    {
        var employee = await employees.GetByIdAsync(dto.EmployeeId);
        if (employee is null) return Err("Employee not found.");
        if (employee.Status is EmploymentStatus.Resigned or EmploymentStatus.Terminated)
            return Err($"{employee.FullName} has left — attendance cannot be recorded.");

        var config = await calendar.GetSettingsAsync(userId);
        var date = (dto.Date ?? DateTime.UtcNow).Date;
        var at = dto.At ?? DateTime.UtcNow;

        if (date > DateTime.UtcNow.Date) return Err("Attendance cannot be recorded for a future date.");
        if (date < employee.HireDate.Date)
            return Err($"That date is before {employee.FullName} was hired ({employee.HireDate:yyyy-MM-dd}).");

        var method = ParseEnum(dto.Method, DefaultMethod(employee.WorkMode));

        // ATT-005 — a field clock-in without a GPS stamp is not evidence of being anywhere.
        var hasGps = dto.Latitude is not null && dto.Longitude is not null;
        if (config.RequireGpsForField && employee.WorkMode == WorkMode.Field && !hasGps
            && method is not ClockInMethod.Manual)
            return Err($"{employee.FullName} is field staff — a GPS location is required to clock in. "
                     + "An HR correction (method Manual) can be recorded without one.");
        if (hasGps && (dto.Latitude is < -90 or > 90 || dto.Longitude is < -180 or > 180))
            return Err("Those GPS coordinates are not on Earth — latitude must be −90..90 and longitude −180..180.");

        var existing = await records.Query().FirstOrDefaultAsync(r => r.EmployeeId == employee.Id && r.Date == date);
        if (existing?.ClockInAt is not null)
            return Err($"{employee.FullName} already clocked in at {existing.ClockInAt:HH:mm} on {date:yyyy-MM-dd}.");

        var isWorkingDay = await calendar.IsWorkingDayAsync(date);
        var holidayName = await HolidayNameAsync(date);

        // A day covered by approved leave is not attendance — someone clocking in on their own leave is almost
        // certainly a mistake, and silently recording it would corrupt both the leave balance and the scorecard.
        var leave = await ApprovedLeaveCoveringAsync(employee.Id, date);
        if (leave is not null)
            return Err($"{employee.FullName} is on approved {leave.LeaveTypeName} on {date:yyyy-MM-dd} "
                     + $"({leave.RequestNumber}). Cancel the leave first if they in fact worked.");

        // Lateness only means something on a working day; weekend and holiday work is recorded, not punished.
        var lateMinutes = 0;
        var status = AttendanceStatus.Present;
        if (!isWorkingDay)
        {
            status = holidayName is not null ? AttendanceStatus.Holiday : AttendanceStatus.NonWorkingDay;
        }
        else
        {
            var minutesIntoDay = (int)at.TimeOfDay.TotalMinutes;
            var lateThreshold = config.WorkDayStartMinutes + config.GraceMinutes;
            if (minutesIntoDay > lateThreshold)
            {
                lateMinutes = minutesIntoDay - config.WorkDayStartMinutes;   // measured from the start, not the grace
                status = AttendanceStatus.Late;
            }
        }

        var record = existing;
        if (record is null)
        {
            record = await records.CreateAsync(new AttendanceRecord
            {
                EmployeeId = employee.Id,
                EmployeeNumber = employee.EmployeeNumber,
                EmployeeName = employee.FullName,
                DepartmentId = employee.DepartmentId,
                DepartmentName = employee.DepartmentName,
                Date = date,
                ClockInAt = at,
                ClockInLatitude = dto.Latitude,
                ClockInLongitude = dto.Longitude,
                ClockInMethod = method,
                WorkMode = employee.WorkMode,
                LateMinutes = lateMinutes,
                Status = status,
                RecordedBy = method == ClockInMethod.Manual ? userId : null,
                Notes = dto.Notes,
                CreatedBy = userId,
                UpdatedBy = userId,
            });
        }
        else
        {
            // The day exists because the sweep already marked it absent — clocking in supersedes that.
            record.ClockInAt = at;
            record.ClockInLatitude = dto.Latitude;
            record.ClockInLongitude = dto.Longitude;
            record.ClockInMethod = method;
            record.WorkMode = employee.WorkMode;
            record.LateMinutes = lateMinutes;
            record.Status = status;
            record.RecordedBy = method == ClockInMethod.Manual ? userId : record.RecordedBy;
            record.Notes = Append(record.Notes, dto.Notes);
            Touch(record, userId);
            await records.UpdateAsync(record);

            await ClearDetectedAbsenceAsync(employee.Id, date, userId,
                $"Clock-in recorded at {at:HH:mm} — the detected absence no longer applies.");
        }

        await LogAsync("AttendanceRecord", record.Id, HrAuditAction.AttendanceClockedIn,
            $"{employee.EmployeeNumber} clocked in at {at:HH:mm} on {date:yyyy-MM-dd} via {method}"
            + (hasGps ? $" at {dto.Latitude},{dto.Longitude}" : string.Empty)
            + (lateMinutes > 0 ? $" — {lateMinutes} minute(s) late." : "."), userId, userName);

        if (lateMinutes > 0)
        {
            await LogAsync("AttendanceRecord", record.Id, HrAuditAction.LateArrivalFlagged,
                $"{employee.EmployeeNumber} was {lateMinutes} minute(s) late on {date:yyyy-MM-dd}.", userId, userName);
            // ATT-002 — the line manager is told. Title carries employee and date so each day's alert is its own
            // (ticketing drops an incoming alert that matches an open one's title).
            await NotifyAsync(tenantSchema, "Attendance", "Warning",
                $"Late arrival — {employee.FullName} on {date:yyyy-MM-dd}",
                $"{employee.EmployeeNumber} clocked in at {at:HH:mm}, {lateMinutes} minute(s) after the "
                + $"{Hhmm(config.WorkDayStartMinutes)} start (grace {config.GraceMinutes} minutes).",
                "hr.manager", employee.UserId);
        }

        var note = status switch
        {
            AttendanceStatus.Late => $" Recorded {lateMinutes} minute(s) late — the line manager has been notified.",
            AttendanceStatus.Holiday => $" {date:yyyy-MM-dd} is {holidayName} — recorded, but not counted as a working day.",
            AttendanceStatus.NonWorkingDay => " That is not a working day — recorded, but not counted towards attendance.",
            _ => " On time.",
        };
        return new AttendanceActionResult("ClockedIn", $"{employee.FullName} clocked in at {at:HH:mm}.{note}", record.Id);
    }

    /// <summary>P27 step 27.4. Hours worked deduct the unpaid lunch break when the day actually spans it.</summary>
    public async Task<AttendanceActionResult> ClockOutAsync(ClockOutDto dto, string userId)
    {
        var employee = await employees.GetByIdAsync(dto.EmployeeId);
        if (employee is null) return Err("Employee not found.");

        var date = (dto.Date ?? DateTime.UtcNow).Date;
        var record = await records.Query().FirstOrDefaultAsync(r => r.EmployeeId == employee.Id && r.Date == date);
        if (record is null || record.ClockInAt is null)
            return Err($"{employee.FullName} has no clock-in on {date:yyyy-MM-dd} to close.");
        if (record.ClockOutAt is not null)
            return Err($"{employee.FullName} already clocked out at {record.ClockOutAt:HH:mm}.");

        var at = dto.At ?? DateTime.UtcNow;
        if (at <= record.ClockInAt.Value)
            return Err($"Clock-out must be after the {record.ClockInAt:HH:mm} clock-in.");
        if (dto.Latitude is not null && (dto.Latitude is < -90 or > 90 || dto.Longitude is < -180 or > 180))
            return Err("Those GPS coordinates are not on Earth.");

        var config = await calendar.GetSettingsAsync(userId);
        record.ClockOutAt = at;
        record.ClockOutLatitude = dto.Latitude;
        record.ClockOutLongitude = dto.Longitude;
        record.HoursWorked = HoursWorked(record.ClockInAt.Value, at, config);
        record.Notes = Append(record.Notes, dto.Notes);
        Touch(record, userId);
        await records.UpdateAsync(record);

        await LogAsync("AttendanceRecord", record.Id, HrAuditAction.AttendanceClockedOut,
            $"{employee.EmployeeNumber} clocked out at {at:HH:mm} on {date:yyyy-MM-dd} — {record.HoursWorked} hour(s) worked.", userId, null);
        return new AttendanceActionResult("ClockedOut",
            $"{employee.FullName} clocked out at {at:HH:mm} — {record.HoursWorked} hour(s) worked.", record.Id);
    }

    // ══ Absences (P27 step 27.5 / ATT-004) ══
    public async Task<List<AbsenceRecordDto>> ListAbsencesAsync(DateTime? from, DateTime? to, string? employeeId, bool? authorised)
    {
        var q = absences.Query().AsNoTracking();
        if (from is not null) q = q.Where(a => a.Date >= from.Value.Date);
        if (to is not null) q = q.Where(a => a.Date <= to.Value.Date);
        if (!string.IsNullOrWhiteSpace(employeeId)) q = q.Where(a => a.EmployeeId == employeeId);
        if (authorised is not null) q = q.Where(a => a.IsAuthorised == authorised);

        var list = await q.OrderByDescending(a => a.Date).Take(1000).ToListAsync();
        return list.Select(ToDto).ToList();
    }

    public async Task<AttendanceActionResult> RecordAbsenceAsync(RecordAbsenceDto dto, string userId)
    {
        var employee = await employees.GetByIdAsync(dto.EmployeeId);
        if (employee is null) return Err("Employee not found.");

        var date = dto.Date.Date;
        if (date > DateTime.UtcNow.Date) return Err("An absence cannot be recorded for a future date.");
        if (date < employee.HireDate.Date)
            return Err($"That date is before {employee.FullName} was hired ({employee.HireDate:yyyy-MM-dd}).");
        if (!await calendar.IsWorkingDayAsync(date))
            return Err($"{date:yyyy-MM-dd} is not a working day — there is nothing to be absent from.");
        if (await absences.Query().AnyAsync(a => a.EmployeeId == employee.Id && a.Date == date))
            return Err($"An absence is already recorded for {employee.FullName} on {date:yyyy-MM-dd}.");

        var attended = await records.Query()
            .FirstOrDefaultAsync(r => r.EmployeeId == employee.Id && r.Date == date && r.ClockInAt != null);
        if (attended is not null)
            return Err($"{employee.FullName} clocked in at {attended.ClockInAt:HH:mm} on {date:yyyy-MM-dd}.");

        var leave = await ApprovedLeaveCoveringAsync(employee.Id, date);
        var authorised = dto.IsAuthorised || leave is not null;

        var record = await CreateAbsenceAsync(employee, date, dto.Reason ?? (leave is not null ? "Approved leave." : "Recorded by HR."),
            authorised, leave, AbsenceSource.RecordedByHr, userId);

        return new AttendanceActionResult("Recorded",
            $"Absence recorded for {employee.FullName} on {date:yyyy-MM-dd}"
            + (authorised
                ? leave is not null ? $" — authorised against {leave.RequestNumber}." : " — authorised, no unpaid days."
                : " — unauthorised, 1 unpaid day pending payroll."), record.Id);
    }

    public async Task<AttendanceActionResult> ExcuseAbsenceAsync(string absenceId, ExcuseAbsenceDto dto, string userId)
    {
        var record = await absences.GetByIdAsync(absenceId);
        if (record is null) return Err("Absence not found.");
        if (record.IsAuthorised) return Err("That absence is already authorised.");
        if (string.IsNullOrWhiteSpace(dto.Reason)) return Err("Excusing an absence needs a reason on the record.");
        if (record.ReleasedToPayrollAt is not null)
            return Err("Payroll has already taken this deduction — reverse it through payroll rather than here.");

        record.IsAuthorised = true;
        record.UnpaidDays = 0;
        record.ExcusedBy = userId;
        record.ExcusedAt = DateTime.UtcNow;
        record.ExcuseNotes = dto.Reason;
        Touch(record, userId);
        await absences.UpdateAsync(record);

        // The day's attendance row should agree with the absence row.
        var day = await records.Query().FirstOrDefaultAsync(r => r.EmployeeId == record.EmployeeId && r.Date == record.Date);
        if (day is not null && day.ClockInAt is null)
        {
            day.Notes = Append(day.Notes, $"Absence excused: {dto.Reason}");
            Touch(day, userId);
            await records.UpdateAsync(day);
        }

        await LogAsync("AbsenceRecord", record.Id, HrAuditAction.AbsenceExcused,
            $"{record.EmployeeNumber}'s absence on {record.Date:yyyy-MM-dd} authorised: {dto.Reason} — unpaid days cleared.", userId, null);
        return new AttendanceActionResult("Excused",
            $"Absence on {record.Date:yyyy-MM-dd} authorised — the unpaid day has been cleared.", record.Id);
    }

    public async Task<List<UnpaidAbsenceDto>> ListUnpaidAbsencesAsync(int? year, int? month)
    {
        var q = absences.Query().AsNoTracking().Where(a => !a.IsAuthorised && a.ReleasedToPayrollAt == null);
        if (year is not null) q = q.Where(a => a.Date.Year == year);
        if (month is not null) q = q.Where(a => a.Date.Month == month);

        var rows = await q.ToListAsync();
        return rows.GroupBy(a => new { a.EmployeeId, a.EmployeeNumber, a.EmployeeName })
            .Select(g => new UnpaidAbsenceDto
            {
                EmployeeId = g.Key.EmployeeId,
                EmployeeNumber = g.Key.EmployeeNumber,
                EmployeeName = g.Key.EmployeeName,
                UnpaidDays = g.Sum(a => a.UnpaidDays),
                AbsenceCount = g.Count(),
                Dates = g.Select(a => a.Date).OrderBy(d => d).ToList(),
            })
            .OrderByDescending(r => r.UnpaidDays).ToList();
    }

    // ══ Settings + holidays ══
    public async Task<AttendanceSettingDto> GetSettingsAsync() => ToDto(await calendar.GetSettingsAsync());

    public async Task<AttendanceActionResult> UpdateSettingsAsync(SaveAttendanceSettingDto dto, string userId)
    {
        var config = await calendar.GetSettingsAsync(userId);

        var start = dto.WorkDayStartMinutes ?? config.WorkDayStartMinutes;
        var end = dto.WorkDayEndMinutes ?? config.WorkDayEndMinutes;
        var grace = dto.GraceMinutes ?? config.GraceMinutes;
        var cutoff = dto.AbsenceCutoffMinutes ?? config.AbsenceCutoffMinutes;

        if (start is < 0 or >= 1440 || end is < 0 or > 1440) return Err("Working-day times must fall within a day.");
        if (end <= start) return Err("The working day must end after it starts.");
        if (grace < 0) return Err("The grace period cannot be negative.");
        if (cutoff <= start)
            return Err("The absence cut-off must be after the start of the working day, or everyone is absent before work begins.");
        if (cutoff < start + grace)
            return Err("The absence cut-off cannot fall inside the grace period — someone would be absent and merely late at once.");
        if ((dto.AbsencePatternThreshold ?? config.AbsencePatternThreshold) < 1) return Err("The absence pattern threshold must be at least 1.");
        if ((dto.AbsencePatternWindowDays ?? config.AbsencePatternWindowDays) < 1) return Err("The absence pattern window must be at least a day.");
        if ((dto.AbsenceBackfillDays ?? config.AbsenceBackfillDays) is < 0 or > 365)
            return Err("The absence backfill window must be between 0 and 365 days.");

        config.WorkDayStartMinutes = start;
        config.WorkDayEndMinutes = end;
        config.LunchStartMinutes = dto.LunchStartMinutes ?? config.LunchStartMinutes;
        config.LunchMinutes = dto.LunchMinutes ?? config.LunchMinutes;
        config.GraceMinutes = grace;
        config.AbsenceCutoffMinutes = cutoff;
        config.WorksMonday = dto.WorksMonday ?? config.WorksMonday;
        config.WorksTuesday = dto.WorksTuesday ?? config.WorksTuesday;
        config.WorksWednesday = dto.WorksWednesday ?? config.WorksWednesday;
        config.WorksThursday = dto.WorksThursday ?? config.WorksThursday;
        config.WorksFriday = dto.WorksFriday ?? config.WorksFriday;
        config.WorksSaturday = dto.WorksSaturday ?? config.WorksSaturday;
        config.WorksSunday = dto.WorksSunday ?? config.WorksSunday;
        config.RequireGpsForField = dto.RequireGpsForField ?? config.RequireGpsForField;
        config.AbsencePatternThreshold = dto.AbsencePatternThreshold ?? config.AbsencePatternThreshold;
        config.AbsencePatternWindowDays = dto.AbsencePatternWindowDays ?? config.AbsencePatternWindowDays;
        config.AbsenceBackfillDays = dto.AbsenceBackfillDays ?? config.AbsenceBackfillDays;

        if (!WorkCalendar.IsWorkingWeekday(config, DayOfWeek.Monday) && !WorkCalendar.IsWorkingWeekday(config, DayOfWeek.Tuesday)
            && !WorkCalendar.IsWorkingWeekday(config, DayOfWeek.Wednesday) && !WorkCalendar.IsWorkingWeekday(config, DayOfWeek.Thursday)
            && !WorkCalendar.IsWorkingWeekday(config, DayOfWeek.Friday) && !WorkCalendar.IsWorkingWeekday(config, DayOfWeek.Saturday)
            && !WorkCalendar.IsWorkingWeekday(config, DayOfWeek.Sunday))
            return Err("At least one day of the week has to be a working day — leave day counts depend on it.");

        Touch(config, userId);
        await settings.UpdateAsync(config);

        await LogAsync("AttendanceSetting", config.Id, HrAuditAction.AttendanceSettingsChanged,
            $"Working time set to {Hhmm(start)}–{Hhmm(end)}, grace {grace} minute(s), absent after {Hhmm(cutoff)}.", userId, null);
        return new AttendanceActionResult("Updated",
            $"Working day is now {Hhmm(start)}–{Hhmm(end)}; late after {Hhmm(start + grace)}, absent after {Hhmm(cutoff)}. "
            + "Leave day counts follow the same working week.", config.Id);
    }

    public async Task<List<PublicHolidayDto>> ListHolidaysAsync(int? year, bool includeInactive)
    {
        var q = holidays.Query().AsNoTracking();
        if (!includeInactive) q = q.Where(h => h.IsActive);
        var list = await q.ToListAsync();

        // A recurring holiday belongs to every year, so it is never filtered out by one.
        if (year is not null) list = list.Where(h => h.IsRecurring || h.Date.Year == year).ToList();

        return list
            .OrderBy(h => h.Date.Month).ThenBy(h => h.Date.Day)
            .Select(h => ToDto(h, year)).ToList();
    }

    public async Task<AttendanceActionResult> CreateHolidayAsync(SavePublicHolidayDto dto, string userId)
    {
        if (string.IsNullOrWhiteSpace(dto.Name)) return Err("A holiday needs a name.");
        if (dto.Date == default) return Err("A holiday needs a date.");

        var date = dto.Date.Date;
        var clash = dto.IsRecurring
            ? await holidays.Query().AnyAsync(h => h.IsRecurring && h.Date.Month == date.Month && h.Date.Day == date.Day)
            : await holidays.Query().AnyAsync(h => !h.IsRecurring && h.Date == date);
        if (clash)
            return Err($"A {(dto.IsRecurring ? "recurring" : "")} holiday is already configured for {date:dd MMM}.".Replace("  ", " "));

        var created = await holidays.CreateAsync(new PublicHoliday
        {
            Date = DateTime.SpecifyKind(date, DateTimeKind.Utc),
            Name = dto.Name.Trim(),
            IsRecurring = dto.IsRecurring,
            IsActive = dto.IsActive ?? true,
            Notes = dto.Notes,
            CreatedBy = userId,
            UpdatedBy = userId,
        });

        await LogAsync("PublicHoliday", created.Id, HrAuditAction.PublicHolidayConfigured,
            $"{created.Name} on {created.Date:dd MMM}{(created.IsRecurring ? " (every year)" : $" {created.Date:yyyy}")} added.", userId, null);
        return new AttendanceActionResult("Created",
            $"{created.Name} added. Leave requests spanning it will stop consuming a day for it.", created.Id);
    }

    public async Task<AttendanceActionResult> UpdateHolidayAsync(string id, SavePublicHolidayDto dto, string userId)
    {
        var holiday = await holidays.GetByIdAsync(id);
        if (holiday is null) return Err("Holiday not found.");
        if (string.IsNullOrWhiteSpace(dto.Name)) return Err("A holiday needs a name.");

        holiday.Date = DateTime.SpecifyKind(dto.Date.Date, DateTimeKind.Utc);
        holiday.Name = dto.Name.Trim();
        holiday.IsRecurring = dto.IsRecurring;
        if (dto.IsActive is not null) holiday.IsActive = dto.IsActive.Value;
        holiday.Notes = dto.Notes;
        Touch(holiday, userId);
        await holidays.UpdateAsync(holiday);

        await LogAsync("PublicHoliday", holiday.Id, HrAuditAction.PublicHolidayConfigured,
            $"{holiday.Name} updated to {holiday.Date:dd MMM}{(holiday.IsRecurring ? " (every year)" : $" {holiday.Date:yyyy}")}.", userId, null);
        return new AttendanceActionResult("Updated", $"{holiday.Name} updated.", holiday.Id);
    }

    public async Task<AttendanceActionResult> DeleteHolidayAsync(string id, string userId)
    {
        var holiday = await holidays.GetByIdAsync(id);
        if (holiday is null) return Err("Holiday not found.");

        // Deactivated rather than deleted: past leave and attendance were counted against this calendar, and
        // removing the row would silently change what those days meant.
        holiday.IsActive = false;
        Touch(holiday, userId);
        await holidays.UpdateAsync(holiday);

        await LogAsync("PublicHoliday", holiday.Id, HrAuditAction.PublicHolidayConfigured,
            $"{holiday.Name} ({holiday.Date:dd MMM}) deactivated.", userId, null);
        return new AttendanceActionResult("Deactivated",
            $"{holiday.Name} is no longer treated as a holiday. Past records keep the calendar they were counted against.", holiday.Id);
    }

    /// <summary>
    /// The Kenyan gazetted fixed-date holidays. Movable ones — Good Friday, Easter Monday, and the two Eids —
    /// are deliberately absent: their dates are declared per year (the Eids by lunar sighting), so guessing them
    /// would put wrong dates in a calendar that silently changes leave balances.
    /// </summary>
    public async Task<AttendanceActionResult> SeedKenyanHolidaysAsync(int year, string userId)
    {
        if (year is < 2000 or > 2100) return Err("That is not a plausible year.");

        var fixedDates = new (int Month, int Day, string Name)[]
        {
            (1, 1, "New Year's Day"),
            (5, 1, "Labour Day"),
            (6, 1, "Madaraka Day"),
            (10, 10, "Utamaduni Day"),
            (10, 20, "Mashujaa Day"),
            (12, 12, "Jamhuri Day"),
            (12, 25, "Christmas Day"),
            (12, 26, "Boxing Day"),
        };

        var existing = await holidays.Query()
            .Where(h => h.IsRecurring)
            .Select(h => new { h.Date.Month, h.Date.Day }).ToListAsync();

        var added = 0;
        foreach (var (month, day, name) in fixedDates)
        {
            if (existing.Any(e => e.Month == month && e.Day == day)) continue;
            await holidays.CreateAsync(new PublicHoliday
            {
                Date = new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Utc),
                Name = name,
                IsRecurring = true,
                IsActive = true,
                Notes = "Gazetted Kenyan public holiday (fixed date).",
                CreatedBy = userId,
                UpdatedBy = userId,
            });
            added++;
        }

        if (added > 0)
            await LogAsync("PublicHoliday", "seed", HrAuditAction.PublicHolidayConfigured,
                $"{added} fixed-date Kenyan public holiday(s) installed.", userId, null);

        return new AttendanceActionResult("Seeded",
            added == 0
                ? "All fixed-date Kenyan holidays are already configured."
                : $"{added} fixed-date holiday(s) installed. Good Friday, Easter Monday and the Eids move each year — "
                + "add them per year as they are declared.");
    }

    // ══ Reporting (P28) ══
    public async Task<List<AttendanceScorecardDto>> ListScorecardsAsync(int? year, string? employeeId, string? departmentId)
    {
        var q = scorecards.Query().AsNoTracking();
        if (year is not null) q = q.Where(s => s.Year == year);
        if (!string.IsNullOrWhiteSpace(employeeId)) q = q.Where(s => s.EmployeeId == employeeId);
        if (!string.IsNullOrWhiteSpace(departmentId)) q = q.Where(s => s.DepartmentId == departmentId);

        var list = await q.OrderByDescending(s => s.Year).ThenByDescending(s => s.Score).ToListAsync();
        return list.Select(ToDto).ToList();
    }

    public async Task<List<AttendanceMonthlyReportDto>> ListMonthlyReportsAsync(int? year, int? month, string? departmentId)
    {
        var q = reports.Query().AsNoTracking();
        if (year is not null) q = q.Where(r => r.Year == year);
        if (month is not null) q = q.Where(r => r.Month == month);
        if (!string.IsNullOrWhiteSpace(departmentId)) q = q.Where(r => r.DepartmentId == departmentId);

        var list = await q.OrderByDescending(r => r.Year).ThenByDescending(r => r.Month)
            .ThenBy(r => r.DepartmentName).ToListAsync();
        return list.Select(ToDto).ToList();
    }

    // ══ The daily sweep ══
    public async Task<AttendanceSweepResultDto> RunAttendanceSweepAsync(string? tenantSchema, string userId)
    {
        var today = DateTime.UtcNow.Date;
        var config = await calendar.GetSettingsAsync(userId);
        var result = new AttendanceSweepResultDto();

        var staff = await employees.Query()
            .Where(e => e.Status != EmploymentStatus.Resigned && e.Status != EmploymentStatus.Terminated)
            .ToListAsync();

        // ── Flag past days left open (clocked in, never out) ──
        // No clock-out is left as a fact, not invented: stamping a fabricated time would put hours nobody worked
        // into the payroll feed. The day is flagged so someone can correct it.
        // The flag is written ONCE — the note is idempotent because the sweep runs daily and an unconditional
        // append would stack the same sentence up every day for as long as the day stayed open.
        const string noClockOutNote = "No clock-out recorded — hours could not be computed.";
        foreach (var open in await records.Query()
                     .Where(r => r.Date < today && r.ClockInAt != null && r.ClockOutAt == null).ToListAsync())
        {
            if (open.Notes is not null && open.Notes.Contains(noClockOutNote, StringComparison.Ordinal)) continue;
            open.Notes = Append(open.Notes, noClockOutNote);
            Touch(open, userId);
            await records.UpdateAsync(open);
            result.DaysClosedWithoutClockOut++;
        }

        // ── Absence detection within the backfill window (P27 step 27.5) ──
        // Today only counts once the cut-off has passed; before that, silence is not yet absence.
        var cutoffPassedToday = DateTime.UtcNow.TimeOfDay.TotalMinutes > config.AbsenceCutoffMinutes;
        var lastDay = cutoffPassedToday ? today : today.AddDays(-1);
        var firstDay = today.AddDays(-Math.Max(0, config.AbsenceBackfillDays));

        if (lastDay >= firstDay)
        {
            var workingDays = await calendar.WorkingDaysBetweenAsync(firstDay, lastDay);

            foreach (var employee in staff)
            {
                var hireDate = employee.HireDate.Date;
                var relevant = workingDays.Where(d => d >= hireDate).ToList();
                if (relevant.Count == 0) continue;

                var attended = (await records.Query()
                        .Where(r => r.EmployeeId == employee.Id && r.Date >= firstDay && r.Date <= lastDay && r.ClockInAt != null)
                        .Select(r => r.Date).ToListAsync()).ToHashSet();
                var alreadyRecorded = (await absences.Query()
                        .Where(a => a.EmployeeId == employee.Id && a.Date >= firstDay && a.Date <= lastDay)
                        .Select(a => a.Date).ToListAsync()).ToHashSet();

                foreach (var day in relevant)
                {
                    if (attended.Contains(day) || alreadyRecorded.Contains(day)) continue;

                    // ATT-004 — reconcile against approved leave before calling it an absence.
                    var leave = await ApprovedLeaveCoveringAsync(employee.Id, day);
                    var record = await CreateAbsenceAsync(employee, day,
                        leave is not null ? $"Approved leave ({leave.RequestNumber})." : "No clock-in recorded.",
                        leave is not null, leave, AbsenceSource.Detected, userId);

                    result.AbsencesDetected++;
                    if (leave is not null) result.AbsencesReconciledToLeave++;

                    if (leave is null)
                        await NotifyAsync(tenantSchema, "Attendance", "Warning",
                            $"Unexplained absence — {employee.FullName} on {day:yyyy-MM-dd}",
                            $"{employee.EmployeeNumber} did not clock in by {Hhmm(config.AbsenceCutoffMinutes)} on {day:d} and has no "
                            + "approved leave for the day. It counts as one unpaid day unless HR excuses it.",
                            "hr.manager", employee.UserId);
                    _ = record;
                }

                // ── ATT-003 pattern alert ──
                var windowStart = today.AddDays(-config.AbsencePatternWindowDays);
                var unauthorised = await absences.Query()
                    .Where(a => a.EmployeeId == employee.Id && !a.IsAuthorised && a.Date >= windowStart)
                    .OrderBy(a => a.Date).ToListAsync();

                if (unauthorised.Count >= config.AbsencePatternThreshold
                    && !unauthorised.Any(a => a.PatternAlertSentAt != null))
                {
                    var trigger = unauthorised.Last();
                    trigger.PatternAlertSentAt = DateTime.UtcNow;
                    Touch(trigger, userId);
                    await absences.UpdateAsync(trigger);
                    result.PatternAlertsRaised++;

                    await LogAsync("AbsenceRecord", trigger.Id, HrAuditAction.AbsencePatternFlagged,
                        $"{employee.EmployeeNumber} reached {unauthorised.Count} unauthorised absence(s) in "
                        + $"{config.AbsencePatternWindowDays} days — pattern alert raised.", userId, null);
                    await NotifyAsync(tenantSchema, "Attendance", "Critical",
                        $"Absence pattern — {employee.FullName} ({unauthorised.Count} in {config.AbsencePatternWindowDays} days)",
                        $"{employee.EmployeeNumber} has {unauthorised.Count} unauthorised absence(s) since "
                        + $"{windowStart:d}: {string.Join(", ", unauthorised.Select(a => a.Date.ToString("d")))}. "
                        + "ATT-003 escalates this to HR.",
                        "hr.approve", employee.UserId);
                }
            }
        }

        // ── Monthly report + scorecards for the prior month (P28) ──
        // Catch-up, like the H3 carry-forward: the row's existence is the guard, so it lands on the 1st and still
        // self-heals if the service was down that day.
        var priorMonth = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-1);
        if (!await reports.Query().AnyAsync(r => r.Year == priorMonth.Year && r.Month == priorMonth.Month))
        {
            var (generated, scored) = await GenerateMonthlyAsync(priorMonth, staff, tenantSchema, userId);
            result.MonthlyReportsGenerated = generated;
            result.ScorecardsUpdated = scored;
        }

        result.Message =
            $"{result.AbsencesDetected} absence(s) detected ({result.AbsencesReconciledToLeave} covered by approved leave), "
            + $"{result.PatternAlertsRaised} pattern alert(s), {result.DaysClosedWithoutClockOut} day(s) with no clock-out, "
            + $"{result.MonthlyReportsGenerated} monthly report(s) and {result.ScorecardsUpdated} scorecard(s) written.";
        return result;
    }

    /// <summary>
    /// P28 steps 28.1–28.5 — the prior month's departmental report and the cumulative annual scorecards.
    /// <para>Both are computed from the attendance and absence rows rather than incremented, so they always agree
    /// with the underlying data even after an absence is excused or a clock-in is corrected.</para>
    /// </summary>
    private async Task<(int Reports, int Scorecards)> GenerateMonthlyAsync(
        DateTime monthStart, List<Employee> staff, string? tenantSchema, string userId)
    {
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);
        var expectedDays = await calendar.WorkingDaysBetweenAsync(monthStart, monthEnd);

        var monthRecords = await records.Query().AsNoTracking()
            .Where(r => r.Date >= monthStart && r.Date <= monthEnd).ToListAsync();
        var monthAbsences = await absences.Query().AsNoTracking()
            .Where(a => a.Date >= monthStart && a.Date <= monthEnd).ToListAsync();

        // ── Departmental reports (ATT-006: each head sees only their own) ──
        var reportCount = 0;
        foreach (var group in staff.GroupBy(e => new { e.DepartmentId, e.DepartmentName }))
        {
            if (await reports.Query().AnyAsync(r => r.Year == monthStart.Year && r.Month == monthStart.Month
                                                 && r.DepartmentId == group.Key.DepartmentId))
                continue;

            var ids = group.Select(e => e.Id).ToHashSet();
            var deptRecords = monthRecords.Where(r => ids.Contains(r.EmployeeId)).ToList();
            var deptAbsences = monthAbsences.Where(a => ids.Contains(a.EmployeeId)).ToList();

            // Expected days are per-employee, because someone hired mid-month was not expected before they joined.
            var deptExpected = group.Sum(e => expectedDays.Count(d => d >= e.HireDate.Date));
            var present = deptRecords.Count(r => r.ClockInAt is not null);
            var late = deptRecords.Count(r => r.Status == AttendanceStatus.Late);
            var unauthorised = deptAbsences.Count(a => !a.IsAuthorised);

            var report = await reports.CreateAsync(new AttendanceMonthlyReport
            {
                DepartmentId = group.Key.DepartmentId,
                DepartmentName = group.Key.DepartmentName ?? "Unassigned",
                Year = monthStart.Year,
                Month = monthStart.Month,
                Headcount = group.Count(),
                ExpectedDays = deptExpected,
                DaysPresent = present,
                DaysLate = late,
                DaysOnLeave = deptAbsences.Count(a => a.IsAuthorised && a.LinkedLeaveRequestId != null),
                AuthorisedAbsences = deptAbsences.Count(a => a.IsAuthorised),
                UnauthorisedAbsences = unauthorised,
                TotalLateMinutes = deptRecords.Sum(r => r.LateMinutes),
                UnpaidDays = deptAbsences.Sum(a => a.UnpaidDays),
                OvertimeHours = 0,        // OVERTIME_REQUEST arrives with H6; the column is ready for it.
                PunctualityRate = Rate(present - late, present),
                AbsenceRate = Rate(unauthorised, deptExpected),
                GeneratedAt = DateTime.UtcNow,
                CreatedBy = userId,
                UpdatedBy = userId,
            });
            reportCount++;

            await LogAsync("AttendanceMonthlyReport", report.Id, HrAuditAction.AttendanceReportGenerated,
                $"{report.DepartmentName} {monthStart:MMMM yyyy}: {present} day(s) present, {late} late, "
                + $"{unauthorised} unauthorised absence(s), {report.UnpaidDays} unpaid day(s).", userId, null);

            await NotifyAsync(tenantSchema, "Attendance", "Warning",
                $"Attendance report ready — {report.DepartmentName}, {monthStart:MMMM yyyy}",
                $"{report.Headcount} staff, {report.PunctualityRate}% punctuality, {unauthorised} unauthorised "
                + $"absence(s) and {report.UnpaidDays} unpaid day(s) for {monthStart:MMMM yyyy}.",
                "hr.manager", null);
        }

        // ── Annual cumulative scorecards (ATT-008) ──
        var year = monthStart.Year;
        var yearStart = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var yearEnd = monthEnd;
        var yearWorkingDays = await calendar.WorkingDaysBetweenAsync(yearStart, yearEnd);
        var yearRecords = await records.Query().AsNoTracking()
            .Where(r => r.Date >= yearStart && r.Date <= yearEnd).ToListAsync();
        var yearAbsences = await absences.Query().AsNoTracking()
            .Where(a => a.Date >= yearStart && a.Date <= yearEnd).ToListAsync();

        var scoreCount = 0;
        foreach (var employee in staff)
        {
            var mine = yearRecords.Where(r => r.EmployeeId == employee.Id).ToList();
            var myAbsences = yearAbsences.Where(a => a.EmployeeId == employee.Id).ToList();
            var expected = yearWorkingDays.Count(d => d >= employee.HireDate.Date);
            if (expected == 0) continue;

            var present = mine.Count(r => r.ClockInAt is not null);
            var late = mine.Count(r => r.Status == AttendanceStatus.Late);
            var unauthorised = myAbsences.Count(a => !a.IsAuthorised);
            var absenceRate = Rate(unauthorised, expected);

            // Punctuality is undefined when nobody attended — there were no arrivals that could have been late.
            // Scoring that as 0% would put a damning number on the appraisal of someone who was on leave for the
            // whole period, or who joined last week, so the score falls back to the attendance component alone
            // and the rate is left at 0 for the UI to render as "no data" (DaysPresent tells it which).
            var punctuality = present > 0 ? Rate(present - late, present) : 0m;
            var score = present > 0
                ? Math.Round(punctuality / 100m * PunctualityWeight + (100m - absenceRate) / 100m * AttendanceWeight, 1)
                : Math.Round(100m - absenceRate, 1);

            var card = await scorecards.Query().FirstOrDefaultAsync(s => s.EmployeeId == employee.Id && s.Year == year);
            var isNew = card is null;
            card ??= new AttendanceScorecard
            {
                EmployeeId = employee.Id,
                Year = year,
                CreatedBy = userId,
            };

            card.EmployeeNumber = employee.EmployeeNumber;
            card.EmployeeName = employee.FullName;
            card.DepartmentId = employee.DepartmentId;
            card.DepartmentName = employee.DepartmentName;
            card.ExpectedDays = expected;
            card.DaysPresent = present;
            card.DaysLate = late;
            card.DaysOnLeave = myAbsences.Count(a => a.IsAuthorised && a.LinkedLeaveRequestId != null);
            card.AuthorisedAbsences = myAbsences.Count(a => a.IsAuthorised);
            card.UnauthorisedAbsences = unauthorised;
            card.TotalLateMinutes = mine.Sum(r => r.LateMinutes);
            card.PunctualityRate = punctuality;
            card.AbsenceRate = absenceRate;
            card.OvertimeHours = 0;       // H6.
            card.Score = score;
            card.ComputedAt = DateTime.UtcNow;
            card.UpdatedBy = userId;
            card.UpdatedAt = DateTime.UtcNow;

            if (isNew) await scorecards.CreateAsync(card);
            else await scorecards.UpdateAsync(card);
            scoreCount++;
        }

        if (scoreCount > 0)
            await LogAsync("AttendanceScorecard", "batch", HrAuditAction.AttendanceScorecardUpdated,
                $"{scoreCount} attendance scorecard(s) recomputed for {year} through {monthEnd:yyyy-MM-dd}.", userId, null);

        return (reportCount, scoreCount);
    }

    // ══ Internals ══

    private async Task<AbsenceRecord> CreateAbsenceAsync(
        Employee employee, DateTime date, string reason, bool authorised, LeaveRequest? leave,
        AbsenceSource source, string userId)
    {
        var record = await absences.CreateAsync(new AbsenceRecord
        {
            EmployeeId = employee.Id,
            EmployeeNumber = employee.EmployeeNumber,
            EmployeeName = employee.FullName,
            DepartmentId = employee.DepartmentId,
            Date = date,
            Reason = reason,
            IsAuthorised = authorised,
            LinkedLeaveRequestId = leave?.Id,
            LinkedLeaveTypeCode = leave?.LeaveTypeCode,
            Source = source,
            UnpaidDays = authorised ? 0 : 1,
            CreatedBy = userId,
            UpdatedBy = userId,
        });

        // The attendance row is the daily picture, so it gets a row for the missed day too — otherwise a blank
        // day is indistinguishable from a day that was never expected.
        var day = await records.Query().FirstOrDefaultAsync(r => r.EmployeeId == employee.Id && r.Date == date);
        if (day is null)
        {
            await records.CreateAsync(new AttendanceRecord
            {
                EmployeeId = employee.Id,
                EmployeeNumber = employee.EmployeeNumber,
                EmployeeName = employee.FullName,
                DepartmentId = employee.DepartmentId,
                DepartmentName = employee.DepartmentName,
                Date = date,
                WorkMode = employee.WorkMode,
                Status = authorised && leave is not null ? AttendanceStatus.OnLeave : AttendanceStatus.Absent,
                Notes = reason,
                CreatedBy = userId,
                UpdatedBy = userId,
            });
        }
        else if (day.ClockInAt is null)
        {
            day.Status = authorised && leave is not null ? AttendanceStatus.OnLeave : AttendanceStatus.Absent;
            day.Notes = Append(day.Notes, reason);
            Touch(day, userId);
            await records.UpdateAsync(day);
        }

        await LogAsync("AbsenceRecord", record.Id, HrAuditAction.AbsenceRecorded,
            $"{employee.EmployeeNumber} absent on {date:yyyy-MM-dd} — {reason}"
            + (authorised ? " (authorised)." : $" (unauthorised, {record.UnpaidDays} unpaid day)."), userId, null);
        return record;
    }

    /// <summary>Drops a detected absence when a clock-in later arrives for the same day — the clock-in is better
    /// evidence than the scheduler's inference. An HR-recorded absence is left alone.</summary>
    private async Task ClearDetectedAbsenceAsync(string employeeId, DateTime date, string userId, string why)
    {
        var absence = await absences.Query()
            .FirstOrDefaultAsync(a => a.EmployeeId == employeeId && a.Date == date && a.Source == AbsenceSource.Detected);
        if (absence is null) return;
        if (absence.ReleasedToPayrollAt is not null) return;   // payroll already acted; leave the trail intact

        absence.IsAuthorised = true;
        absence.UnpaidDays = 0;
        absence.ExcuseNotes = Append(absence.ExcuseNotes, why);
        absence.ExcusedBy = userId;
        absence.ExcusedAt = DateTime.UtcNow;
        Touch(absence, userId);
        await absences.UpdateAsync(absence);

        await LogAsync("AbsenceRecord", absence.Id, HrAuditAction.AbsenceExcused,
            $"{absence.EmployeeNumber}'s detected absence on {date:yyyy-MM-dd} cleared — {why}", userId, null);
    }

    private Task<LeaveRequest?> ApprovedLeaveCoveringAsync(string employeeId, DateTime date)
        => leaveRequests.Query().AsNoTracking()
            .FirstOrDefaultAsync(r => r.EmployeeId == employeeId
                                   && r.Status == LeaveRequestStatus.Approved
                                   && r.StartDate <= date && r.EndDate >= date);

    private async Task<string?> HolidayNameAsync(DateTime date)
    {
        var rows = await holidays.Query().AsNoTracking().Where(h => h.IsActive).ToListAsync();
        return rows.FirstOrDefault(h => h.IsRecurring
                ? h.Date.Month == date.Month && h.Date.Day == date.Day
                : h.Date.Date == date.Date)
            ?.Name;
    }

    /// <summary>Elapsed time less the lunch break, but only when the day actually spans lunch — someone who
    /// leaves at noon never took it.</summary>
    private static decimal HoursWorked(DateTime inAt, DateTime outAt, AttendanceSetting config)
    {
        var minutes = (decimal)(outAt - inAt).TotalMinutes;
        var lunchStart = inAt.Date.AddMinutes(config.LunchStartMinutes);
        var lunchEnd = lunchStart.AddMinutes(config.LunchMinutes);
        if (inAt < lunchStart && outAt > lunchEnd) minutes -= config.LunchMinutes;
        return Math.Round(Math.Max(0, minutes) / 60m, 2);
    }

    private static decimal Rate(int numerator, int denominator)
        => denominator <= 0 ? 0 : Math.Round(numerator * 100m / denominator, 1);

    private static ClockInMethod DefaultMethod(WorkMode mode)
        => mode == WorkMode.Field ? ClockInMethod.Mobile : ClockInMethod.Desktop;

    private static string Hhmm(int minutes) => $"{minutes / 60:00}:{minutes % 60:00}";

    /// <summary>See the note on <c>ProbationService.NotifyAsync</c>: ticketing drops an incoming alert that shares
    /// an open one's (tenant, source, title), so every title here carries the employee and the date.</summary>
    private async Task NotifyAsync(string? schema, string source, string severity, string title, string message,
        string? permission, string? assignedToUserId)
    {
        if (string.IsNullOrWhiteSpace(schema)) return;
        await notifier.CreateAlertAsync(schema, source, severity, title, message, permission, assignedToUserId);
    }

    private static AttendanceRecordDto ToDto(AttendanceRecord r) => new()
    {
        Id = r.Id, EmployeeId = r.EmployeeId, EmployeeNumber = r.EmployeeNumber, EmployeeName = r.EmployeeName,
        DepartmentName = r.DepartmentName, Date = r.Date, ClockInAt = r.ClockInAt, ClockOutAt = r.ClockOutAt,
        ClockInLatitude = r.ClockInLatitude, ClockInLongitude = r.ClockInLongitude,
        ClockOutLatitude = r.ClockOutLatitude, ClockOutLongitude = r.ClockOutLongitude,
        ClockInMethod = r.ClockInMethod?.ToString(), WorkMode = r.WorkMode.ToString(),
        LateMinutes = r.LateMinutes, Status = r.Status.ToString(), HoursWorked = r.HoursWorked,
        RecordedBy = r.RecordedBy, Notes = r.Notes,
        HasGps = r.ClockInLatitude is not null && r.ClockInLongitude is not null,
        MissingClockOut = r.ClockInAt is not null && r.ClockOutAt is null,
    };

    private static AbsenceRecordDto ToDto(AbsenceRecord a) => new()
    {
        Id = a.Id, EmployeeId = a.EmployeeId, EmployeeNumber = a.EmployeeNumber, EmployeeName = a.EmployeeName,
        Date = a.Date, Reason = a.Reason, IsAuthorised = a.IsAuthorised,
        LinkedLeaveRequestId = a.LinkedLeaveRequestId, LinkedLeaveTypeCode = a.LinkedLeaveTypeCode,
        Source = a.Source.ToString(), UnpaidDays = a.UnpaidDays, ReleasedToPayrollAt = a.ReleasedToPayrollAt,
        PatternAlertSentAt = a.PatternAlertSentAt,
        ExcusedBy = a.ExcusedBy, ExcusedAt = a.ExcusedAt, ExcuseNotes = a.ExcuseNotes,
    };

    private static AttendanceSettingDto ToDto(AttendanceSetting c) => new()
    {
        Id = c.Id,
        WorkDayStartMinutes = c.WorkDayStartMinutes, WorkDayEndMinutes = c.WorkDayEndMinutes,
        LunchStartMinutes = c.LunchStartMinutes, LunchMinutes = c.LunchMinutes,
        GraceMinutes = c.GraceMinutes, AbsenceCutoffMinutes = c.AbsenceCutoffMinutes,
        WorksMonday = c.WorksMonday, WorksTuesday = c.WorksTuesday, WorksWednesday = c.WorksWednesday,
        WorksThursday = c.WorksThursday, WorksFriday = c.WorksFriday,
        WorksSaturday = c.WorksSaturday, WorksSunday = c.WorksSunday,
        RequireGpsForField = c.RequireGpsForField,
        AbsencePatternThreshold = c.AbsencePatternThreshold,
        AbsencePatternWindowDays = c.AbsencePatternWindowDays,
        AbsenceBackfillDays = c.AbsenceBackfillDays,
        WorkDayStart = Hhmm(c.WorkDayStartMinutes), WorkDayEnd = Hhmm(c.WorkDayEndMinutes),
        LateAfter = Hhmm(c.WorkDayStartMinutes + c.GraceMinutes), AbsentAfter = Hhmm(c.AbsenceCutoffMinutes),
    };

    /// <summary>A recurring holiday is shown against the year being viewed, not the year its row happens to
    /// carry — otherwise Christmas would read as 2026 forever.</summary>
    private static PublicHolidayDto ToDto(PublicHoliday h, int? viewYear) => new()
    {
        Id = h.Id,
        Date = h.IsRecurring && viewYear is not null
            ? new DateTime(viewYear.Value, h.Date.Month, h.Date.Day, 0, 0, 0, DateTimeKind.Utc)
            : h.Date,
        Name = h.Name, IsRecurring = h.IsRecurring, IsActive = h.IsActive, Notes = h.Notes,
    };

    private static AttendanceScorecardDto ToDto(AttendanceScorecard s) => new()
    {
        Id = s.Id, EmployeeId = s.EmployeeId, EmployeeNumber = s.EmployeeNumber, EmployeeName = s.EmployeeName,
        DepartmentName = s.DepartmentName, Year = s.Year, ExpectedDays = s.ExpectedDays,
        DaysPresent = s.DaysPresent, DaysLate = s.DaysLate, DaysOnLeave = s.DaysOnLeave,
        AuthorisedAbsences = s.AuthorisedAbsences, UnauthorisedAbsences = s.UnauthorisedAbsences,
        TotalLateMinutes = s.TotalLateMinutes, PunctualityRate = s.PunctualityRate, AbsenceRate = s.AbsenceRate,
        OvertimeHours = s.OvertimeHours, Score = s.Score, ComputedAt = s.ComputedAt,
    };

    private static AttendanceMonthlyReportDto ToDto(AttendanceMonthlyReport r) => new()
    {
        Id = r.Id, DepartmentId = r.DepartmentId, DepartmentName = r.DepartmentName,
        Year = r.Year, Month = r.Month,
        Period = new DateTime(r.Year, r.Month, 1).ToString("MMMM yyyy"),
        Headcount = r.Headcount, ExpectedDays = r.ExpectedDays, DaysPresent = r.DaysPresent,
        DaysLate = r.DaysLate, DaysOnLeave = r.DaysOnLeave,
        AuthorisedAbsences = r.AuthorisedAbsences, UnauthorisedAbsences = r.UnauthorisedAbsences,
        TotalLateMinutes = r.TotalLateMinutes, UnpaidDays = r.UnpaidDays, OvertimeHours = r.OvertimeHours,
        PunctualityRate = r.PunctualityRate, AbsenceRate = r.AbsenceRate, GeneratedAt = r.GeneratedAt,
    };

    private static TEnum ParseEnum<TEnum>(string? value, TEnum fallback) where TEnum : struct
        => Enum.TryParse<TEnum>(value, true, out var parsed) ? parsed : fallback;

    private static string? Append(string? existing, string? addition)
        => string.IsNullOrWhiteSpace(addition) ? existing
         : string.IsNullOrWhiteSpace(existing) ? addition : $"{existing} {addition}";

    private static AttendanceActionResult Err(string message) => new("Error", message);
    private static void Touch(BaseEntity e, string userId) { e.UpdatedBy = userId; e.UpdatedAt = DateTime.UtcNow; }

    private async Task LogAsync(string entityType, string entityId, HrAuditAction action, string detail, string userId, string? userName)
    {
        await audit.CreateAsync(new HrAuditLog
        {
            EntityType = entityType, EntityId = entityId, Action = action, Detail = detail,
            PerformedBy = userId, PerformedByName = userName, OccurredAt = DateTime.UtcNow,
            CreatedBy = userId, UpdatedBy = userId,
        });
    }
}
