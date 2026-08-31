namespace TicketingService.Core.Services;

/// #16 — business-hours SLA. Adds a number of *working* hours to a start instant, skipping nights
/// and non-working days, so an SLA filed Friday 5pm doesn't breach over the weekend. Times are
/// stored UTC; business hours are defined in the tenant's local zone via a fixed UTC offset
/// (Kenya = +3, no DST). Configurable via the "BusinessHours" section; sensible defaults otherwise.
public class BusinessCalendar
{
    public double UtcOffsetHours { get; init; } = 3;      // EAT
    public int StartHour { get; init; } = 8;              // 08:00 local
    public int EndHour { get; init; } = 17;               // 17:00 local
    public HashSet<DayOfWeek> WorkDays { get; init; } = new()
    {
        DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday
    };

    /// D2-3 — public-holiday overrides (ISO dates, e.g. "2026-12-12"). A holiday is treated as a
    /// non-working day, so SLA clocks skip it just like a weekend. Configured under "BusinessHours".
    public List<string> Holidays { get; init; } = new();

    private HashSet<DateOnly>? _holidaySet;
    private HashSet<DateOnly> HolidaySet => _holidaySet ??= Holidays
        .Select(h => DateOnly.TryParse(h, out var d) ? d : (DateOnly?)null)
        .Where(d => d.HasValue).Select(d => d!.Value).ToHashSet();

    private bool IsWorkingDay(DateTime localDate) =>
        WorkDays.Contains(localDate.DayOfWeek) && !HolidaySet.Contains(DateOnly.FromDateTime(localDate));

    private double WorkHoursPerDay => EndHour - StartHour;

    /// <summary>Add <paramref name="hours"/> working hours to <paramref name="startUtc"/>, returning UTC.</summary>
    public DateTime AddWorkingHours(DateTime startUtc, double hours)
    {
        if (hours <= 0 || WorkDays.Count == 0 || WorkHoursPerDay <= 0)
            return startUtc.AddHours(hours);   // degenerate config → fall back to wall-clock

        var local = startUtc.AddHours(UtcOffsetHours);
        var remaining = TimeSpan.FromHours(hours);

        // Guard against pathological configs: cap the walk well beyond any realistic SLA.
        for (var guard = 0; remaining > TimeSpan.Zero && guard < 100_000; guard++)
        {
            var dayStart = local.Date.AddHours(StartHour);
            var dayEnd   = local.Date.AddHours(EndHour);

            if (!IsWorkingDay(local) || local >= dayEnd)
            {
                local = NextWorkDayStart(local);   // roll to next working day's opening
                continue;
            }
            if (local < dayStart)
            {
                local = dayStart;                  // before opening → start of this day's window
                continue;
            }

            var available = dayEnd - local;        // time left in today's window
            if (remaining <= available)
                return local.Add(remaining).AddHours(-UtcOffsetHours);

            remaining -= available;
            local = dayEnd;                        // consumed today; loop rolls to next day
        }

        return local.AddHours(-UtcOffsetHours);
    }

    private DateTime NextWorkDayStart(DateTime local)
    {
        var next = local.Date.AddDays(1);
        // Cap the search at ~2 years so a misconfigured all-holidays calendar can't spin forever.
        for (var guard = 0; !IsWorkingDay(next) && guard < 800; guard++) next = next.AddDays(1);
        return next.AddHours(StartHour);
    }
}
