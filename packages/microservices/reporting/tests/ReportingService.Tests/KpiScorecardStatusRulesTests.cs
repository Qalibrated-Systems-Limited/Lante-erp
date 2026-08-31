using FluentAssertions;
using ReportingService.Core.Entities;
using ReportingService.Core.Enums;
using ReportingService.Core.Services;
using Xunit;

namespace ReportingService.Tests;

/// <summary>
/// The red/amber/green rule, extracted from KpiScorecardsController so the KPI Scorecard Progress report
/// (#225) shares it rather than reimplementing it.
///
/// <para>Direction is inferred from the scorecard's own threshold ordering rather than a flag, which is
/// elegant and easy to get backwards — so it is worth pinning in both directions. A lower-is-better
/// metric scored as higher-is-better reports a crisis as healthy.</para>
/// </summary>
public class KpiScorecardStatusRulesTests
{
    // Higher is better: utilisation %. Target 80, warn below 80, critical below 60.
    private static KpiScorecard HigherBetter() =>
        new() { Name = "Fleet utilisation", TargetValue = 80m, WarningThreshold = 70m, CriticalThreshold = 60m };

    // Lower is better: overdue receivables. Target 0, warn above 0, critical above 500k.
    private static KpiScorecard LowerBetter() =>
        new() { Name = "Overdue receivables", TargetValue = 0m, WarningThreshold = 100_000m, CriticalThreshold = 500_000m };

    [Fact]
    public void Direction_is_inferred_from_the_threshold_ordering()
    {
        KpiScorecardStatusRules.HigherIsBetter(HigherBetter()).Should().BeTrue();
        KpiScorecardStatusRules.HigherIsBetter(LowerBetter()).Should().BeFalse();
    }

    [Theory]
    [InlineData(95, ScorecardStatus.OnTarget)]
    [InlineData(80, ScorecardStatus.OnTarget)]     // exactly on target counts as met
    [InlineData(79, ScorecardStatus.Warning)]
    [InlineData(60, ScorecardStatus.Warning)]      // exactly at critical is still warning
    [InlineData(59, ScorecardStatus.Critical)]
    [InlineData(0, ScorecardStatus.Critical)]
    public void A_higher_is_better_metric_grades_downwards(decimal value, ScorecardStatus expected)
        => KpiScorecardStatusRules.Resolve(HigherBetter(), value).Should().Be(expected);

    [Theory]
    [InlineData(0, ScorecardStatus.OnTarget)]
    [InlineData(1, ScorecardStatus.Warning)]
    [InlineData(500_000, ScorecardStatus.Warning)]
    [InlineData(500_001, ScorecardStatus.Critical)]
    public void A_lower_is_better_metric_grades_upwards(decimal value, ScorecardStatus expected)
        // If this ever inverted, half a million in overdue receivables would report as OnTarget.
        => KpiScorecardStatusRules.Resolve(LowerBetter(), value).Should().Be(expected);

    [Fact]
    public void Variance_keeps_its_sign()
    {
        // Not absolute: the sign is the only thing saying whether a miss is a shortfall or an overshoot,
        // and for a lower-is-better metric positive IS the bad direction.
        KpiScorecardStatusRules.Variance(HigherBetter(), 75m).Should().Be(-5m);
        KpiScorecardStatusRules.Variance(LowerBetter(), 250_000m).Should().Be(250_000m);
    }

    [Fact]
    public void A_scorecard_with_equal_target_and_warning_is_treated_as_higher_is_better()
    {
        // The boundary of the inference. Target == Warning is ambiguous by construction; it resolves to
        // higher-is-better, and a caller wanting the other reading must order its thresholds to say so.
        var s = new KpiScorecard { TargetValue = 50m, WarningThreshold = 50m, CriticalThreshold = 40m };
        KpiScorecardStatusRules.HigherIsBetter(s).Should().BeTrue();
        KpiScorecardStatusRules.Resolve(s, 50m).Should().Be(ScorecardStatus.OnTarget);
        KpiScorecardStatusRules.Resolve(s, 45m).Should().Be(ScorecardStatus.Warning);
    }
}
