using ReportingService.Core.Enums;

namespace ReportingService.Core.Entities;

// RPT-010: a monitored target for one DataSource. WarningThreshold/CriticalThreshold's ordering
// relative to TargetValue determines direction (see KpiScorecardsController.ResolveStatus) — a
// metric where higher is better (e.g. utilisation %) has Critical < Warning < Target, one where
// lower is better (e.g. overdue receivables) has Critical > Warning > Target.
public class KpiScorecard : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string DataSourceId { get; set; } = string.Empty;
    public decimal TargetValue { get; set; }
    public decimal WarningThreshold { get; set; }
    public decimal CriticalThreshold { get; set; }
    public ScorecardPeriod Period { get; set; }
}
