using ComplianceService.Core.Services;
using FluentAssertions;
using Xunit;

namespace ComplianceService.Tests;

/// <summary>
/// STAT-010's RAG banding and the overdue test, on the compliance dashboard.
///
/// <para>Both were instant-based and are now date-based. The banding used <c>TotalDays</c>, a double,
/// so a deadline exactly 30 calendar days out read <b>Amber at 00:00 and Red from 00:01 onwards</b> —
/// Amber for one instant of the day the spec says it should be Amber throughout. Two replicas whose
/// clocks differ near a boundary could disagree about the same deadline, which is exactly what
/// fleet-service's <c>ExpiryBand</c> was written to prevent.</para>
///
/// <para>These are the numbers a compliance officer plans a month around. A filing that shows Amber
/// in the morning and Red in the afternoon teaches people to distrust the colour.</para>
/// </summary>
public class StatutoryRagTests
{
    private static DateTime At(int y, int m, int d, int hour = 9) => new(y, m, d, hour, 0, 0, DateTimeKind.Utc);

    // ── The bands ───────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(61, "Green")]
    [InlineData(90, "Green")]
    [InlineData(60, "Amber")]
    [InlineData(45, "Amber")]
    [InlineData(30, "Amber")]
    [InlineData(29, "Red")]
    [InlineData(1, "Red")]
    [InlineData(0, "Red")]
    [InlineData(-1, "Red")]
    [InlineData(-400, "Red")]
    public void The_band_is_the_documented_threshold(int daysOut, string expected)
    {
        var now = At(2026, 8, 20);

        // STAT-010: GREEN over 60 days, AMBER 30-60, RED under 30 or overdue. Both boundaries are
        // inclusive-Amber, which is what the spec's "30-60d" says and what a `<=` or `<` slip breaks.
        StatutoryDashboardService.Rag(now.AddDays(daysOut), now).Should().Be(expected);
    }

    // ── The defect: the band must not move with the clock ───────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(9)]
    [InlineData(12)]
    [InlineData(18)]
    [InlineData(23)]
    public void A_deadline_thirty_calendar_days_out_is_Amber_all_day(int hour)
    {
        var due = At(2026, 9, 19, hour: 0);
        var now = At(2026, 8, 20, hour);

        // The bug: with fractional TotalDays this was Amber only at 00:00 and Red for the remaining
        // 23 hours and 59 minutes — the same deadline, the same day, a different colour depending on
        // when the page was opened.
        StatutoryDashboardService.Rag(due, now).Should().Be("Amber");
    }

    [Fact]
    public void The_band_does_not_depend_on_the_time_of_day_at_either_boundary()
    {
        // Swept across both boundaries at every hour, because a fractional-day comparison fails at
        // whichever one you did not think to check.
        foreach (var daysOut in new[] { 29, 30, 31, 59, 60, 61 })
        {
            var bands = Enumerable.Range(0, 24)
                .Select(h => StatutoryDashboardService.Rag(At(2026, 8, 20, 0).AddDays(daysOut), At(2026, 8, 20, h)))
                .Distinct()
                .ToList();

            bands.Should().ContainSingle(
                $"a deadline {daysOut} days out must read the same all day, not shift with the clock");
        }
    }

    [Fact]
    public void Two_replicas_moments_apart_agree_on_the_band()
    {
        // The reason ExpiryBand exists in fleet-service, applied here: a pod whose clock sits a
        // second either side of midnight must not disagree with its neighbour about a filing.
        var due = At(2026, 9, 19, 0);
        var a = new DateTime(2026, 8, 20, 23, 59, 59, DateTimeKind.Utc);
        var b = new DateTime(2026, 8, 21, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(-1);

        StatutoryDashboardService.Rag(due, a).Should().Be(StatutoryDashboardService.Rag(due, b));
    }

    // ── Overdue ─────────────────────────────────────────────────────────────────

    [Fact]
    public void A_filing_due_today_is_not_yet_overdue()
    {
        var today = At(2026, 8, 20, 0);

        // Previously `DueDate < now` compared instants, so a return due today at midnight showed as
        // overdue from 00:00:01 — while the whole working day remained available to file it. Telling
        // someone they have missed a deadline they can still meet is worse than saying nothing.
        StatutoryDashboardService.IsPastDue(today, At(2026, 8, 20, 9)).Should().BeFalse();
        StatutoryDashboardService.IsPastDue(today, At(2026, 8, 20, 23)).Should().BeFalse();
    }

    [Fact]
    public void A_filing_due_yesterday_is_overdue()
    {
        StatutoryDashboardService.IsPastDue(At(2026, 8, 19, 23), At(2026, 8, 20, 0)).Should().BeTrue();
    }

    [Fact]
    public void A_future_filing_is_not_overdue()
    {
        StatutoryDashboardService.IsPastDue(At(2026, 8, 21, 0), At(2026, 8, 20, 23)).Should().BeFalse();
    }

    [Fact]
    public void Overdue_and_Red_agree_once_a_deadline_has_passed()
    {
        // The two are separate computations shown side by side on the same dashboard. Anything
        // overdue must also read Red, or the row contradicts itself.
        var now = At(2026, 8, 20);
        foreach (var daysOut in new[] { -1, -10, -365 })
        {
            var due = now.AddDays(daysOut);
            StatutoryDashboardService.IsPastDue(due, now).Should().BeTrue();
            StatutoryDashboardService.Rag(due, now).Should().Be("Red");
        }
    }
}
