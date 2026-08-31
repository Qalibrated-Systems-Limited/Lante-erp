using ReportingService.Core.Enums;

namespace ReportingService.Core.DTOs;

/// <summary>
/// Report #10 — KPI Scorecard Progress. Every configured scorecard measured against its target in one
/// place, so a manager can see what is off without opening each one.
/// </summary>
public class KpiScorecardProgressReportDto
{
    public KpiScorecardProgressSummaryDto Summary { get; set; } = new();
    public List<KpiScorecardProgressRowDto> Scorecards { get; set; } = new();

    /// <summary>Non-fatal problems — an unresolvable metric, a deleted data source. The report is built
    /// from whatever did resolve rather than failing whole, because one broken metric should not hide
    /// the other nineteen.</summary>
    public List<string> Warnings { get; set; } = new();
}

public class KpiScorecardProgressSummaryDto
{
    public int Total { get; set; }
    public int OnTarget { get; set; }
    public int Warning { get; set; }
    public int Critical { get; set; }

    /// <summary>Scorecards whose current value could not be measured at all. Counted separately and NOT
    /// folded into Critical: "we do not know" and "it is bad" are different facts, and a dashboard that
    /// conflates them turns a broken integration into a false alarm — or hides one behind a real one.</summary>
    public int Unmeasured { get; set; }
}

public class KpiScorecardProgressRowDto
{
    public string ScorecardId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? DataSourceName { get; set; }
    public string MetricKey { get; set; } = string.Empty;
    public ScorecardPeriod Period { get; set; }

    public decimal TargetValue { get; set; }
    public decimal WarningThreshold { get; set; }
    public decimal CriticalThreshold { get; set; }

    /// <summary>Null when the metric could not be resolved — see <see cref="Error"/>. Deliberately
    /// nullable rather than defaulting to zero: a zero reads as a measured value, and for most metrics
    /// zero is either perfect or catastrophic.</summary>
    public decimal? CurrentValue { get; set; }
    public decimal? Variance { get; set; }

    /// <summary>Null when unmeasured, for the same reason as <see cref="CurrentValue"/>.</summary>
    public ScorecardStatus? Status { get; set; }

    /// <summary>Whether a higher number is the good direction, so a caller does not have to re-derive it
    /// from the threshold ordering to render an arrow.</summary>
    public bool HigherIsBetter { get; set; }

    public string? Error { get; set; }
}
