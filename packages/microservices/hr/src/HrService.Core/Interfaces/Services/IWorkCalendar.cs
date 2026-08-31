using HrService.Core.Entities;

namespace HrService.Core.Interfaces.Services;

/// <summary>
/// H4 — the single answer to "is this a working day", shared by leave (H3) and attendance (H4).
/// <para>It exists because both phases need the same rule and must not drift: if leave counted Christmas as a
/// working day while attendance treated it as a holiday, an employee would be charged a leave day for a day the
/// attendance report says they were never expected. One calculator, one answer.</para>
/// </summary>
public interface IWorkCalendar
{
    /// <summary>The tenant's working-time rules, creating the defaults on first use so no tenant is left without
    /// a configuration and every call has something to work from.</summary>
    Task<AttendanceSetting> GetSettingsAsync(string? userId = null);

    /// <summary>Holiday dates in the range, with recurring holidays expanded across the years it spans.</summary>
    Task<HashSet<DateTime>> HolidayDatesAsync(DateTime from, DateTime to);

    /// <summary>False for weekends (per the tenant's configured week) and public holidays.</summary>
    Task<bool> IsWorkingDayAsync(DateTime date);

    /// <summary>Working days in an inclusive range — weekends and public holidays excluded.</summary>
    Task<int> CountWorkingDaysAsync(DateTime from, DateTime to);

    /// <summary>Every working day in an inclusive range, for the sweeps that walk day by day.</summary>
    Task<List<DateTime>> WorkingDaysBetweenAsync(DateTime from, DateTime to);
}
