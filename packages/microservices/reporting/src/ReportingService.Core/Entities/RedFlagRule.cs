using ReportingService.Core.Enums;

namespace ReportingService.Core.Entities;

// RPT-011: fires a RedFlagEvent whenever the bound DataSource's current value satisfies
// Condition against ThresholdValue (e.g. TRIR GreaterThan 2.5). Evaluated by
// RedFlagEvaluationBackgroundService, not on-demand.
public class RedFlagRule : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string DataSourceId { get; set; } = string.Empty;
    public RedFlagCondition Condition { get; set; }
    public decimal ThresholdValue { get; set; }
    public RedFlagSeverity Severity { get; set; }
    public bool IsActive { get; set; } = true;
}
