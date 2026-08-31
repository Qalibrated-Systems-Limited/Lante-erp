using FinanceService.Core.Services;
using FluentAssertions;
using Xunit;

namespace FinanceService.Tests;

/// <summary>
/// The monthly depreciation charge (#230 section 2).
///
/// <para><b>Why only the arithmetic.</b> <c>DepreciationService.RunDepreciationAsync</c> runs inside a
/// transaction holding a <c>pg_advisory_xact_lock</c>. Neither the in-memory provider (no
/// transactions, no raw SQL) nor SQLite (no such function) can honour that, which is why this service
/// had no tests at all. The two remaining bullets on #230 — "running it twice does not double-post"
/// and "disposal stops further depreciation" — are database-level properties and need a real Postgres
/// fixture, not an extension of LedgerFixture. Said plainly so nobody reads this file as covering
/// them.</para>
///
/// <para>The arithmetic is what silently produces a wrong number on the balance sheet, and it does not
/// need a database.</para>
/// </summary>
public class DepreciationRulesTests
{
    // ── The rate convention ─────────────────────────────────────────────────────

    [Fact]
    public void The_annual_rate_is_a_fraction_not_a_percentage()
    {
        // 20% a year on 120,000 is 2,000 a month. If the rate were read as a percentage the same
        // input would charge 200,000 — the asset gone in under a month.
        //
        // This is worth an explicit test because the codebase uses BOTH conventions: HR seeds NSSF as
        // `6m` meaning 6% and divides by 100, while the asset categories here seed 0.20 meaning 20%
        // and do not. Someone moving between the two services will eventually seed 20 here.
        DepreciationRules.MonthlyCharge(0.20m, 120_000m, 0m).Should().Be(2_000m);
    }

    [Theory]
    [InlineData(0.3333, 90_000, 2_499.75)]   // IT equipment, 3 years
    [InlineData(0.20, 1_200_000, 20_000)]    // motor vehicles, 5 years
    [InlineData(0.125, 96_000, 1_000)]       // furniture, 8 years
    [InlineData(0.15, 240_000, 3_000)]       // plant, 6-7 years
    public void The_seeded_category_rates_produce_the_expected_monthly_charge(
        decimal rate, decimal cost, decimal expected)
    {
        // Driven with the rates FixedAssetService actually seeds, so a change to those constants
        // shows up here as a number rather than as a diff nobody re-derives.
        DepreciationRules.MonthlyCharge(rate, cost, 0m).Should().Be(expected);
    }

    [Fact]
    public void A_zero_rate_category_never_charges_anything()
    {
        // Leasehold Improvements is seeded at 0m — depreciated over the lease term, not by this run.
        DepreciationRules.MonthlyCharge(0m, 500_000m, 0m).Should().Be(0m);
    }

    // ── The cap at remaining book value ─────────────────────────────────────────

    [Fact]
    public void The_final_month_charges_only_the_stub_that_is_left()
    {
        // 2,000 a month, but only 750 of book value remains. Charging the full 2,000 would take the
        // asset below zero and overstate the expense in its last month.
        DepreciationRules.MonthlyCharge(0.20m, 120_000m, 119_250m).Should().Be(750m);
    }

    [Fact]
    public void A_fully_depreciated_asset_charges_nothing()
    {
        DepreciationRules.MonthlyCharge(0.20m, 120_000m, 120_000m).Should().Be(0m);
    }

    [Fact]
    public void An_over_depreciated_asset_charges_nothing_rather_than_a_negative()
    {
        // Reachable through a manual adjustment to AccumulatedDepreciation. Without the Math.Max the
        // charge goes negative, which posts a CREDIT to depreciation expense and quietly writes the
        // asset back up.
        DepreciationRules.MonthlyCharge(0.20m, 120_000m, 130_000m).Should().Be(0m);
    }

    [Fact]
    public void Depreciation_is_charged_on_cost_not_on_the_written_down_value()
    {
        // Straight line, not reducing balance. Half depreciated, the charge is unchanged — a
        // reducing-balance implementation would give 1,000 here.
        DepreciationRules.MonthlyCharge(0.20m, 120_000m, 60_000m).Should().Be(2_000m);
    }

    // ── Rounding ────────────────────────────────────────────────────────────────

    [Theory]
    // 200 / 12 recurs; unrounded it is 16.666…7 and would carry that into the ledger.
    [InlineData(0.20, 1_000, 16.67)]
    // 333.3 / 12 = 27.775 exactly — a true midpoint, so this also pins the rounding MODE.
    [InlineData(0.3333, 1_000, 27.78)]
    public void The_charge_is_rounded_to_two_decimal_places(decimal rate, decimal cost, decimal expected)
    {
        // The first version of this test used 0.3333 × 10,000 / 12, which is exactly 277.75 — it
        // needed no rounding, so it passed against a mutant that removed the rounding entirely. A
        // test of rounding has to use a value that actually rounds.
        DepreciationRules.MonthlyCharge(rate, cost, 0m).Should().Be(expected);
    }

    [Fact]
    public void Rounding_happens_before_the_remaining_value_cap_not_after()
    {
        // The rounded monthly charge (277.75) exceeds the 100 left, so 100 is charged exactly —
        // not a rounded version of the cap, and not the uncapped figure.
        DepreciationRules.MonthlyCharge(0.3333m, 10_000m, 9_900m).Should().Be(100m);
    }

    // ── Period parsing ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData("2026-01", 2026, 1, 31)]
    [InlineData("2026-02", 2026, 2, 28)]   // not a leap year
    [InlineData("2024-02", 2024, 2, 29)]   // leap year
    [InlineData("2026-04", 2026, 4, 30)]
    [InlineData("2026-12", 2026, 12, 31)]
    public void A_period_posts_on_its_last_day(string period, int y, int m, int d)
    {
        DepreciationRules.LastDayOfPeriod(period)
            .Should().Be(new DateTime(y, m, d, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void The_posting_date_is_UTC()
    {
        // The column is timestamptz; an Unspecified Kind is written as local time by Npgsql and at
        // +03:00 a month-end date shifts into the next period.
        DepreciationRules.LastDayOfPeriod("2026-07").Kind.Should().Be(DateTimeKind.Utc);
    }

    [Theory]
    [InlineData("2026")]
    [InlineData("2026-07-15")]
    [InlineData("July 2026")]
    [InlineData("")]
    [InlineData("abcd-ef")]
    public void A_malformed_period_is_refused_with_a_usable_message(string period)
    {
        var act = () => DepreciationRules.LastDayOfPeriod(period);

        act.Should().Throw<InvalidOperationException>().WithMessage("*yyyy-MM*");
    }

    [Theory]
    [InlineData("2026-13")]
    [InlineData("2026-00")]
    public void An_impossible_month_is_refused_the_same_way(string period)
    {
        // Previously these reached DateTime.DaysInMonth and threw ArgumentOutOfRangeException, which
        // surfaces as a 500 with no hint that the period string was the problem.
        var act = () => DepreciationRules.LastDayOfPeriod(period);

        act.Should().Throw<InvalidOperationException>().WithMessage("*yyyy-MM*");
    }
}
