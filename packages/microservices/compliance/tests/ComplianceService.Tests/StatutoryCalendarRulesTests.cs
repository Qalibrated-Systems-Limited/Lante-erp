using ComplianceService.Core.Enums;
using ComplianceService.Core.Services;
using FluentAssertions;
using Xunit;

namespace ComplianceService.Tests;

/// <summary>
/// STAT-001's month selection and due-date clamping — the first tests in compliance-service.
///
/// <para>The service had <b>174 source files and zero tests</b>. That is how the Npgsql translation
/// crash in this same method reached production: its own fix commit says it "never surfaced before
/// because tenant_qsl had zero active Monthly/Quarterly StatutoryObligations rows to exercise the
/// query". The fix shipped without a test, so the surrounding logic was still unexercised.</para>
///
/// <para>Getting this wrong is a late statutory filing and a penalty, not a bug report. The whole
/// point of generating a period ahead is that an obligation appears on the calendar <i>before</i> it
/// is due — so "which months" is the assertion that matters.</para>
/// </summary>
public class StatutoryCalendarRulesTests
{
    private static DateTime Utc(int y, int m, int d) => new(y, m, d, 0, 0, 0, DateTimeKind.Utc);

    private static string[] Months(ObligationFrequency f, DateTime now) =>
        StatutoryCalendarRules.CandidateMonths(f, now).Select(d => d.ToString("yyyy-MM")).ToArray();

    // ── Monthly ─────────────────────────────────────────────────────────────────

    [Fact]
    public void A_monthly_obligation_gets_this_month_and_the_next()
    {
        Months(ObligationFrequency.Monthly, Utc(2026, 5, 15))
            .Should().Equal("2026-05", "2026-06");
    }

    [Fact]
    public void A_monthly_obligation_rolls_into_the_next_year_in_December()
    {
        // The simplest year-boundary case, and the one the quarterly branch got wrong.
        Months(ObligationFrequency.Monthly, Utc(2026, 12, 3))
            .Should().Equal("2026-12", "2027-01");
    }

    [Fact]
    public void The_month_returned_is_always_the_first_of_the_month_at_midnight_UTC()
    {
        // The caller compares deadlines with a month-start/month-end range, so a candidate carrying
        // a day or a time would land the generated deadline outside the window that checks for it —
        // and the job would create a duplicate every run.
        var months = StatutoryCalendarRules.CandidateMonths(ObligationFrequency.Monthly, Utc(2026, 5, 29));
        months.Should().OnlyContain(m => m.Day == 1 && m.TimeOfDay == TimeSpan.Zero);
        months.Should().OnlyContain(m => m.Kind == DateTimeKind.Utc);
    }

    // ── Quarterly ───────────────────────────────────────────────────────────────

    /// <summary>
    /// The rule, stated once: from the PREVIOUS month forward, take the next two quarter-ends.
    ///
    /// <para>The previous month rather than the current one is deliberate and predates this change —
    /// it re-ensures the quarter-end that has just passed, so a sweep that did not run still
    /// self-heals. Insertion is idempotent, so re-ensuring costs nothing.</para>
    ///
    /// <para>February through October are unchanged from the old implementation. January and the
    /// last two months are not — see the two tests below.</para>
    /// </summary>
    [Theory]
    [InlineData(1,  "2025-12", "2026-03")]
    [InlineData(2,  "2026-03", "2026-06")]
    [InlineData(3,  "2026-03", "2026-06")]
    [InlineData(4,  "2026-03", "2026-06")]
    [InlineData(5,  "2026-06", "2026-09")]
    [InlineData(6,  "2026-06", "2026-09")]
    [InlineData(7,  "2026-06", "2026-09")]
    [InlineData(8,  "2026-09", "2026-12")]
    [InlineData(9,  "2026-09", "2026-12")]
    [InlineData(10, "2026-09", "2026-12")]
    [InlineData(11, "2026-12", "2027-03")]
    [InlineData(12, "2026-12", "2027-03")]
    public void A_quarterly_obligation_always_gets_two_quarter_ends(int month, string first, string second)
    {
        Months(ObligationFrequency.Quarterly, Utc(2026, month, 10))
            .Should().Equal(first, second);
    }

    [Fact]
    public void January_now_catches_up_the_previous_December_like_every_other_post_quarter_month()
    {
        // A behaviour change worth naming. April re-ensures March, July re-ensures June, October
        // re-ensures September — but January used to jump straight to March, because the old
        // implementation filtered a list of THIS calendar year's quarter-ends and December belonged
        // to the previous one. January was the only post-quarter month that skipped its catch-up,
        // and only by accident of the year filter.
        Months(ObligationFrequency.Quarterly, Utc(2026, 1, 10))
            .Should().Equal("2025-12", "2026-03");
    }

    [Theory]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(12)]
    public void Quarterly_generation_crosses_the_year_boundary(int month)
    {
        // The defect. The previous implementation filtered a fixed list of THIS calendar year's
        // quarter-ends, so from November it had none left but December: it returned a single month,
        // and next March did not appear on the calendar until January. For two months every year the
        // "one period ahead" guarantee silently stopped holding, on filings whose whole value is
        // being seen in advance.
        var months = Months(ObligationFrequency.Quarterly, Utc(2026, month, 10));

        months.Should().HaveCount(2);
        months.Should().Contain("2026-12");
        if (month >= 11) months.Should().Contain("2027-03");
    }

    [Fact]
    public void December_looks_forward_to_next_March_rather_than_stopping()
    {
        Months(ObligationFrequency.Quarterly, Utc(2026, 12, 31))
            .Should().Equal("2026-12", "2027-03");
    }

    [Fact]
    public void Quarterly_months_are_only_ever_quarter_ends()
    {
        // Across a whole year, nothing but March/June/September/December may be produced — a
        // rolling cursor that lost its `% 3` test would generate a deadline every month and quietly
        // quadruple the obligation.
        for (var m = 1; m <= 12; m++)
        {
            var months = StatutoryCalendarRules.CandidateMonths(ObligationFrequency.Quarterly, Utc(2026, m, 15));
            months.Should().HaveCount(2, $"month {m} must still produce a period ahead");
            months.Should().OnlyContain(d => d.Month % 3 == 0, $"month {m} produced a non-quarter month");
            months.Should().BeInAscendingOrder();
        }
    }

    [Fact]
    public void A_quarter_end_month_includes_itself_rather_than_skipping_to_the_next()
    {
        // In June, June's own deadline must still be ensured — the job may not have run yet this
        // quarter. Starting the walk at `now` rather than `now - 1 month` would skip it.
        Months(ObligationFrequency.Quarterly, Utc(2026, 6, 30)).Should().Contain("2026-06");
    }

    // ── Due-date clamping ───────────────────────────────────────────────────────

    [Fact]
    public void A_due_day_inside_the_month_is_used_as_is()
    {
        StatutoryCalendarRules.DueDateFor(20, Utc(2026, 5, 1)).Should().Be(Utc(2026, 5, 20));
    }

    [Theory]
    [InlineData(2026, 2, 28)]   // 2026 is not a leap year
    [InlineData(2024, 2, 29)]   // 2024 is
    [InlineData(2026, 4, 30)]
    public void A_due_day_past_the_end_of_the_month_is_clamped(int year, int month, int expectedDay)
    {
        // An obligation due "on the 31st" must land on the last day of a shorter month rather than
        // throwing — which is what constructing the date directly would do.
        StatutoryCalendarRules.DueDateFor(31, Utc(year, month, 1))
            .Should().Be(new DateTime(year, month, expectedDay, 0, 0, 0, DateTimeKind.Utc));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void A_nonsense_due_day_is_floored_to_the_first(int day)
    {
        // StatutoryDay is a plain int with no validation behind it. Day 0 would throw inside
        // DateTime, taking the whole background sweep down for every tenant — which is exactly how
        // this job failed before.
        StatutoryCalendarRules.DueDateFor(day, Utc(2026, 5, 1)).Should().Be(Utc(2026, 5, 1));
    }

    [Fact]
    public void The_due_date_is_UTC_so_it_round_trips_through_a_timestamptz_column()
    {
        // The column is timestamptz. An Unspecified Kind is written as local time by Npgsql, which
        // shifts the date across midnight for +03:00 and would file the deadline in the wrong month.
        StatutoryCalendarRules.DueDateFor(15, Utc(2026, 5, 1)).Kind.Should().Be(DateTimeKind.Utc);
    }
}
