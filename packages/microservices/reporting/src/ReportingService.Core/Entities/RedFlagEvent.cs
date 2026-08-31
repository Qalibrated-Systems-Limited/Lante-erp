using ReportingService.Core.Enums;

namespace ReportingService.Core.Entities;

// RPT-012: one open-until-resolved occurrence of a RedFlagRule's condition being true.
// DaysOpen is refreshed by RedFlagEvaluationBackgroundService on every check while still Open;
// ResolvedAt/Status flip to Resolved the first check where the condition no longer holds.
public class RedFlagEvent : BaseEntity
{
    public string RedFlagRuleId { get; set; } = string.Empty;
    public DateTime TriggeredAt { get; set; }
    public RedFlagSeverity Severity { get; set; }
    public decimal Value { get; set; }
    public int DaysOpen { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public RedFlagEventStatus Status { get; set; } = RedFlagEventStatus.Open;
}
