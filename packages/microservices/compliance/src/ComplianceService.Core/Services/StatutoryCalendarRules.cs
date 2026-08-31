using ComplianceService.Core.Enums;

namespace ComplianceService.Core.Services;

/// <summary>
/// Which months a recurring statutory obligation needs a deadline row for, and what date that
/// deadline falls on.
///
/// <para>Extracted from <c>StatutoryCalendarBackgroundService</c> so it can be tested. Both methods
/// are pure — no database, no clock of their own — and they are the part of STAT-001 that can be
/// wrong in a way nobody notices until a filing is late. The service around them is plumbing; this
/// is the judgement.</para>
///
/// <para>Compliance had no tests at all when this was extracted (174 source files, zero), which is
/// also how the Npgsql translation crash in this same method reached production: it "never surfaced
/// before because tenant_qsl had zero active Monthly/Quarterly StatutoryObligations rows to exercise
/// the query".</para>
/// </summary>
public static class StatutoryCalendarRules
{
    /// <summary>
    /// The months to ensure a deadline exists for: the current period and one ahead, so an
    /// obligation is visible on the calendar before it is due.
    /// </summary>
    /// <param name="frequency">Monthly or Quarterly. Annual obligations are handled elsewhere.</param>
    /// <param name="now">The current date, passed in rather than read, so a year boundary can be tested.</param>
    /// <returns>Month starts, ascending, in UTC.</returns>
    public static IReadOnlyList<DateTime> CandidateMonths(ObligationFrequency frequency, DateTime now)
    {
        var thisMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        if (frequency == ObligationFrequency.Monthly)
            return new[] { thisMonth, thisMonth.AddMonths(1) };

        // Quarterly. Walked forward from the previous month rather than filtered out of a fixed
        // list of this calendar year's quarter-ends.
        //
        // The fixed-year version produced only ONE month in November and December — the year's
        // remaining quarter-ends had run out, and next March was not a candidate because it is not
        // in `now.Year`. So for two months every year the "one period ahead" guarantee silently
        // stopped holding, and a Q1 filing first appeared on the calendar in January instead of
        // November. Advance visibility is the entire point of generating a period ahead.
        var results = new List<DateTime>();
        var cursor = thisMonth.AddMonths(-1);
        while (results.Count < 2)
        {
            if (cursor.Month % 3 == 0 && cursor >= thisMonth.AddMonths(-1))
                results.Add(cursor);
            cursor = cursor.AddMonths(1);
        }
        return results;
    }

    /// <summary>
    /// The obligation's due date within a given month.
    ///
    /// <para>The statutory day is clamped to the month's length, so an obligation due on the 31st
    /// falls on the 28th or 29th in February rather than throwing.</para>
    /// </summary>
    public static DateTime DueDateFor(int statutoryDay, DateTime month)
    {
        var day = Math.Min(Math.Max(statutoryDay, 1), DateTime.DaysInMonth(month.Year, month.Month));
        return new DateTime(month.Year, month.Month, day, 0, 0, 0, DateTimeKind.Utc);
    }
}
