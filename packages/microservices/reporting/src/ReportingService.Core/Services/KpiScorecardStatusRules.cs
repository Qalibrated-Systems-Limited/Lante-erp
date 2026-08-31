using ReportingService.Core.Entities;
using ReportingService.Core.Enums;

namespace ReportingService.Core.Services;

/// <summary>
/// Where a measured value sits against a scorecard's target and thresholds.
///
/// <para>Extracted from <c>KpiScorecardsController</c> rather than copied, because the KPI Scorecard
/// Progress report (#225) needs the same judgement across every scorecard. Two implementations of a
/// red/amber/green rule drift, and the one that drifts is the one nobody re-reads — the same shape as the
/// thirteen permission hierarchies in #204 and the sixteen .dockerignore copies in #241.</para>
/// </summary>
public static class KpiScorecardStatusRules
{
    /// <summary>
    /// Direction is inferred from the scorecard's own threshold ordering rather than a separate flag:
    /// <c>TargetValue &gt;= WarningThreshold</c> means higher is better (utilisation %), and
    /// <c>TargetValue &lt; WarningThreshold</c> means lower is better (overdue receivables).
    /// </summary>
    public static bool HigherIsBetter(KpiScorecard s) => s.TargetValue >= s.WarningThreshold;

    public static ScorecardStatus Resolve(KpiScorecard s, decimal value)
    {
        if (HigherIsBetter(s))
            return value >= s.TargetValue ? ScorecardStatus.OnTarget
                 : value >= s.CriticalThreshold ? ScorecardStatus.Warning
                 : ScorecardStatus.Critical;

        return value <= s.TargetValue ? ScorecardStatus.OnTarget
             : value <= s.CriticalThreshold ? ScorecardStatus.Warning
             : ScorecardStatus.Critical;
    }

    /// <summary>
    /// Signed distance from target, in the metric's own units. Deliberately NOT absolute: the sign is the
    /// only thing that says whether a miss is an overshoot or a shortfall, and for a lower-is-better
    /// metric a positive variance is the bad direction.
    /// </summary>
    public static decimal Variance(KpiScorecard s, decimal value) => value - s.TargetValue;
}
