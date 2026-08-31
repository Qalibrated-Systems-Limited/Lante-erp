using Microsoft.EntityFrameworkCore;
using HrService.Core.Entities;
using HrService.Core.Interfaces.Repositories;
using HrService.Core.Interfaces.Services;

namespace HrService.Core.Services;

/// <summary>
/// H4 — implementation of the shared working-day rule. See <see cref="IWorkCalendar"/> for why leave and
/// attendance must share it.
/// </summary>
public class WorkCalendar(
    IGenericRepository<AttendanceSetting> settings,
    IGenericRepository<PublicHoliday> holidays) : IWorkCalendar
{
    public async Task<AttendanceSetting> GetSettingsAsync(string? userId = null)
    {
        var existing = await settings.Query().FirstOrDefaultAsync();
        if (existing is not null) return existing;

        // First use: install the ATT-001 defaults rather than returning nothing and forcing every caller to
        // cope with an unconfigured tenant.
        return await settings.CreateAsync(new AttendanceSetting
        {
            CreatedBy = userId ?? "system",
            UpdatedBy = userId ?? "system",
        });
    }

    public async Task<HashSet<DateTime>> HolidayDatesAsync(DateTime from, DateTime to)
    {
        var rows = await holidays.Query().AsNoTracking().Where(h => h.IsActive).ToListAsync();
        var dates = new HashSet<DateTime>();

        foreach (var h in rows)
        {
            if (!h.IsRecurring)
            {
                if (h.Date.Date >= from.Date && h.Date.Date <= to.Date) dates.Add(h.Date.Date);
                continue;
            }

            // A fixed-date holiday is one row covering every year, so project it onto each year the range spans.
            for (var year = from.Year; year <= to.Year; year++)
            {
                // 29 February is only a real date in a leap year; skip rather than throw.
                if (h.Date.Month == 2 && h.Date.Day == 29 && !DateTime.IsLeapYear(year)) continue;
                var candidate = new DateTime(year, h.Date.Month, h.Date.Day, 0, 0, 0, DateTimeKind.Utc);
                if (candidate >= from.Date && candidate <= to.Date) dates.Add(candidate);
            }
        }
        return dates;
    }

    public async Task<bool> IsWorkingDayAsync(DateTime date)
    {
        var config = await GetSettingsAsync();
        if (!IsWorkingWeekday(config, date.DayOfWeek)) return false;
        return !(await HolidayDatesAsync(date, date)).Contains(date.Date);
    }

    public async Task<int> CountWorkingDaysAsync(DateTime from, DateTime to)
        => (await WorkingDaysBetweenAsync(from, to)).Count;

    public async Task<List<DateTime>> WorkingDaysBetweenAsync(DateTime from, DateTime to)
    {
        var start = from.Date;
        var end = to.Date;
        if (end < start) return [];

        var config = await GetSettingsAsync();
        var holidayDates = await HolidayDatesAsync(start, end);

        var days = new List<DateTime>();
        for (var d = start; d <= end; d = d.AddDays(1))
            if (IsWorkingWeekday(config, d.DayOfWeek) && !holidayDates.Contains(d))
                days.Add(d);
        return days;
    }

    public static bool IsWorkingWeekday(AttendanceSetting c, DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => c.WorksMonday,
        DayOfWeek.Tuesday => c.WorksTuesday,
        DayOfWeek.Wednesday => c.WorksWednesday,
        DayOfWeek.Thursday => c.WorksThursday,
        DayOfWeek.Friday => c.WorksFriday,
        DayOfWeek.Saturday => c.WorksSaturday,
        _ => c.WorksSunday,
    };
}
