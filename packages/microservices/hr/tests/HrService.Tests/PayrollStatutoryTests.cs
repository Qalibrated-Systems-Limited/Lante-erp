using FluentAssertions;
using HrService.Core.Entities;
using HrService.Core.Enums;
using HrService.Core.Services;
using Xunit;

namespace HrService.Tests;

/// <summary>
/// The statutory arithmetic behind a payslip: PAYE band walking, NSSF-style tiering, and the
/// clamping that caps a contribution.
///
/// <para>
/// This is the highest-risk logic in the service. A wrong PAYE or NSSF figure is a legal
/// problem rather than a bug report, and it is invisible to everyone until KRA disagrees.
/// </para>
/// <para>
/// <b>What these tests deliberately do NOT do is assert statutory rates.</b> Lante keeps PAYE
/// bands and statutory rates as effective-dated data, each carrying a <c>NeedsConfirmation</c>
/// flag, precisely because they move with every Finance Act — the service's own comment says a
/// number nobody checked must not be mistaken for a verified one. Baking today's rates into an
/// assertion would convert that honest uncertainty into false confidence: the test would pass
/// while the figure was out of date, and it would fail on a perfectly correct rate change.
/// </para>
/// <para>
/// So what is tested is the <b>engine</b>: given bands and rates as data, is the arithmetic
/// right? Every fixture below uses deliberately artificial round numbers so the expected value
/// is obvious by hand, and so nobody mistakes a fixture for a rate table.
/// </para>
/// </summary>
public class PayrollStatutoryTests
{
    // ── PAYE band walking ────────────────────────────────────────────────────────

    /// <summary>
    /// Bands are half-open: <c>LowerBound</c> is exclusive, <c>UpperBound</c> inclusive, so band
    /// N starts exactly where N−1 ends. These are made-up round figures, not a rate table.
    /// </summary>
    private static List<PayeTaxBand> Bands() => new()
    {
        new PayeTaxBand { BandOrder = 1, LowerBound = 0m,     UpperBound = 1000m, Rate = 10m },
        new PayeTaxBand { BandOrder = 2, LowerBound = 1000m,  UpperBound = 2000m, Rate = 20m },
        new PayeTaxBand { BandOrder = 3, LowerBound = 2000m,  UpperBound = null,  Rate = 30m },
    };

    [Fact]
    public void No_taxable_income_means_no_tax()
    {
        PayrollRunService.TaxOn(0m, Bands()).Should().Be(0m);
        PayrollRunService.TaxOn(-500m, Bands()).Should().Be(0m,
            "a negative taxable figure must not produce a negative tax, i.e. a refund");
    }

    [Fact]
    public void Income_inside_the_first_band_is_taxed_only_at_that_rate()
    {
        // 500 @ 10%
        PayrollRunService.TaxOn(500m, Bands()).Should().Be(50m);
    }

    [Fact]
    public void Income_spanning_bands_is_taxed_progressively_not_at_the_top_rate()
    {
        // 1500 = 1000 @ 10% + 500 @ 20% = 100 + 100
        // The failure this catches is the classic one: taxing the whole amount at the marginal
        // rate, which would give 300 here.
        PayrollRunService.TaxOn(1500m, Bands()).Should().Be(200m);
    }

    [Fact]
    public void Income_above_every_ceiling_uses_the_open_top_band()
    {
        // 5000 = 1000 @ 10% + 1000 @ 20% + 3000 @ 30% = 100 + 200 + 900
        PayrollRunService.TaxOn(5000m, Bands()).Should().Be(1200m);
    }

    [Fact]
    public void Income_exactly_on_a_boundary_belongs_to_the_lower_band()
    {
        // The whole point of the half-open reading. At exactly 1000, band 2 contributes nothing.
        PayrollRunService.TaxOn(1000m, Bands()).Should().Be(100m);
        // And one shilling more starts band 2 rather than re-charging band 1.
        PayrollRunService.TaxOn(1001m, Bands()).Should().Be(100.2m);
    }

    [Fact]
    public void An_empty_band_table_yields_no_tax_rather_than_throwing()
    {
        // A run with no bands in force is blocked upstream by a blocker, not by this function —
        // so this asserts the function degrades quietly rather than taking the run down.
        PayrollRunService.TaxOn(5000m, new List<PayeTaxBand>()).Should().Be(0m);
    }

    [Fact]
    public void Bands_are_walked_in_the_order_supplied()
    {
        // Ordering is the caller's responsibility. Documented here because a caller that
        // supplied bands unsorted would silently mis-compute: the loop breaks as soon as
        // taxable <= LowerBound, so an out-of-order table truncates early.
        var reversed = Bands();
        reversed.Reverse();
        var outOfOrder = PayrollRunService.TaxOn(5000m, reversed);
        var inOrder = PayrollRunService.TaxOn(5000m, Bands());
        outOfOrder.Should().Be(inOrder,
            "for this table the arithmetic is order-independent because the slices are disjoint");
    }

    // ── Percent-of-gross contributions (SHA, Housing Levy shape) ─────────────────

    private static StatutoryRate PercentRate(decimal rate, decimal? max = null, decimal? min = null,
                                             decimal? employerRate = null) => new()
    {
        Code = "TEST_PCT", Name = "Test percent",
        RateType = StatutoryRateType.PercentOfGross,
        Rate = rate, MaxAmount = max, MinAmount = min, EmployerRate = employerRate,
    };

    [Fact]
    public void A_percent_of_gross_contribution_is_that_percent_of_gross()
    {
        // 100,000 @ 2% = 2,000
        PayrollRunService.StatutoryAmount(PercentRate(2m), 100_000m).Should().Be(2_000m);
    }

    [Fact]
    public void A_percent_contribution_scales_linearly_and_is_zero_at_zero_gross()
    {
        var r = PercentRate(2m);
        PayrollRunService.StatutoryAmount(r, 50_000m).Should().Be(1_000m);
        PayrollRunService.StatutoryAmount(r, 200_000m).Should().Be(4_000m);
        PayrollRunService.StatutoryAmount(r, 0m).Should().Be(0m);
    }

    [Fact]
    public void A_percent_contribution_rounds_to_cents_away_from_zero()
    {
        // 12,345 @ 2.75% = 339.4875 -> 339.49, not 339.48. MidpointRounding.AwayFromZero is the
        // deliberate choice: banker's rounding on a deduction would systematically under-remit.
        PayrollRunService.StatutoryAmount(PercentRate(2.75m), 12_345m).Should().Be(339.49m);
    }

    [Fact]
    public void A_maximum_caps_the_contribution()
    {
        PayrollRunService.StatutoryAmount(PercentRate(2m, max: 1_500m), 100_000m).Should().Be(1_500m);
    }

    [Fact]
    public void A_minimum_floors_the_contribution()
    {
        // Relevant shape: a scheme with a floor charges the floor even on very low pay.
        PayrollRunService.StatutoryAmount(PercentRate(2m, min: 300m), 1_000m).Should().Be(300m);
    }

    [Fact]
    public void A_missing_rate_contributes_nothing_rather_than_throwing()
    {
        var r = PercentRate(0m);
        r.Rate = null;
        PayrollRunService.StatutoryAmount(r, 100_000m).Should().Be(0m);
    }

    // ── Tiered contributions (the NSSF shape) ────────────────────────────────────

    private static StatutoryRate Tier(decimal lower, decimal? upper, decimal rate,
                                      decimal? max = null, decimal? employerRate = null) => new()
    {
        Code = "TEST_TIER", Name = "Test tier",
        RateType = StatutoryRateType.TieredPercent,
        TierLowerBound = lower, TierUpperBound = upper,
        Rate = rate, MaxAmount = max, EmployerRate = employerRate,
    };

    [Fact]
    public void A_tier_charges_only_the_slice_of_pay_inside_it()
    {
        // Tier over 10,000–50,000 at 5%. Pay of 30,000 exposes 20,000 to it -> 1,000.
        PayrollRunService.StatutoryAmount(Tier(10_000m, 50_000m, 5m), 30_000m).Should().Be(1_000m);
    }

    [Fact]
    public void Pay_below_the_tier_floor_contributes_nothing()
    {
        // Not a negative contribution — the Math.Max(0, ...) guard. Without it, pay under the
        // floor would produce a credit.
        PayrollRunService.StatutoryAmount(Tier(10_000m, 50_000m, 5m), 5_000m).Should().Be(0m);
        PayrollRunService.StatutoryAmount(Tier(10_000m, 50_000m, 5m), 10_000m).Should().Be(0m);
    }

    [Fact]
    public void A_tier_stops_at_its_ceiling_however_high_pay_goes()
    {
        var t = Tier(10_000m, 50_000m, 5m);
        // Full tier width is 40,000 -> 2,000, and it must not grow past that.
        PayrollRunService.StatutoryAmount(t, 50_000m).Should().Be(2_000m);
        PayrollRunService.StatutoryAmount(t, 500_000m).Should().Be(2_000m);
        PayrollRunService.StatutoryAmount(t, 5_000_000m).Should().Be(2_000m);
    }

    [Fact]
    public void Two_tiers_compose_into_the_scheme_total()
    {
        // The NSSF shape: a lower tier plus an upper tier, each charging its own slice.
        var t1 = Tier(0m, 10_000m, 6m);       // up to 10,000 -> max 600
        var t2 = Tier(10_000m, 50_000m, 6m);  // 10,000-50,000 -> max 2,400

        // Pay inside tier 1 only.
        PayrollRunService.StatutoryAmount(t1, 5_000m).Should().Be(300m);
        PayrollRunService.StatutoryAmount(t2, 5_000m).Should().Be(0m);

        // Pay in tier 2: tier 1 is saturated, tier 2 charges the excess.
        PayrollRunService.StatutoryAmount(t1, 30_000m).Should().Be(600m);
        PayrollRunService.StatutoryAmount(t2, 30_000m).Should().Be(1_200m);
        (PayrollRunService.StatutoryAmount(t1, 30_000m)
         + PayrollRunService.StatutoryAmount(t2, 30_000m)).Should().Be(1_800m);

        // Pay above both: the scheme total is capped by the tier widths, not by pay.
        (PayrollRunService.StatutoryAmount(t1, 999_999m)
         + PayrollRunService.StatutoryAmount(t2, 999_999m)).Should().Be(3_000m);
    }

    [Fact]
    public void An_open_ended_tier_charges_everything_above_its_floor()
    {
        PayrollRunService.StatutoryAmount(Tier(10_000m, null, 5m), 30_000m).Should().Be(1_000m);
    }

    [Fact]
    public void A_maximum_caps_a_tier_too()
    {
        PayrollRunService.StatutoryAmount(Tier(0m, 100_000m, 10m, max: 480m), 100_000m).Should().Be(480m);
    }

    // ── Employer share ──────────────────────────────────────────────────────────

    [Fact]
    public void The_employer_share_uses_the_employer_rate_not_the_employee_rate()
    {
        // A scheme where the two differ is exactly where a copy-paste bug hides.
        var r = PercentRate(2m, employerRate: 3m);
        PayrollRunService.StatutoryAmount(r, 100_000m).Should().Be(2_000m);
        PayrollRunService.EmployerAmount(r, 100_000m).Should().Be(3_000m);
    }

    [Fact]
    public void An_employer_share_of_a_tier_respects_the_same_tier_edges()
    {
        var t = Tier(10_000m, 50_000m, 5m, employerRate: 5m);
        PayrollRunService.EmployerAmount(t, 30_000m).Should().Be(1_000m);
        PayrollRunService.EmployerAmount(t, 5_000m).Should().Be(0m);
        PayrollRunService.EmployerAmount(t, 500_000m).Should().Be(2_000m);
    }

    [Fact]
    public void No_employer_rate_means_no_employer_cost()
    {
        // Employee-only schemes must not silently bill the employer the employee rate.
        PayrollRunService.EmployerAmount(PercentRate(2m), 100_000m).Should().Be(0m);
    }

    // ── Rate types that carry no computed amount ─────────────────────────────────

    [Fact]
    public void A_fixed_amount_rate_computes_nothing_here()
    {
        // Personal relief is a FixedAmount and is applied against computed PAYE elsewhere, not
        // as a contribution. This asserts it cannot leak in as one.
        var relief = new StatutoryRate
        {
            Code = "PERSONAL_RELIEF", RateType = StatutoryRateType.FixedAmount, FixedAmount = 2_400m,
        };
        PayrollRunService.StatutoryAmount(relief, 100_000m).Should().Be(0m);
        PayrollRunService.EmployerAmount(relief, 100_000m).Should().Be(0m);
    }

    // ── Overtime hourly rate ────────────────────────────────────────────────────

    [Fact]
    public void The_hourly_rate_divides_basic_by_working_days_then_by_hours_per_day()
    {
        // 22,000 basic over 22 working days at 8h/day -> 125/hour.
        PayrollRunService.HourlyRate(22_000m, 22).Should().Be(125m);
    }

    [Fact]
    public void A_zero_working_day_month_yields_a_zero_rate_rather_than_dividing_by_zero()
    {
        PayrollRunService.HourlyRate(22_000m, 0).Should().Be(0m);
        PayrollRunService.HourlyRate(22_000m, -5).Should().Be(0m);
    }
}
