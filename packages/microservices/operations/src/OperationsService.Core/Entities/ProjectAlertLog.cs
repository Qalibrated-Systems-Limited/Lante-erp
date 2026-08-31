namespace OperationsService.Core.Entities;

/// <summary>
/// O2 — PROJECT_ALERT_LOG: an audit record of a budget-burn threshold being crossed (80/90/95/100%).
/// Written once per threshold by the background sweep so alerts are traceable and not re-sent. The
/// 100% row corresponds to the hard budget lock.
/// </summary>
public class ProjectAlertLog : BaseEntity
{
    public string ProjectId       { get; set; } = string.Empty;
    public int    ThresholdPct    { get; set; }   // 80 | 90 | 95 | 100
    public decimal SpentAmount    { get; set; }
    public decimal CommittedAmount { get; set; }
    public decimal BudgetAmount   { get; set; }
    public decimal BurnPct        { get; set; }
    public string  Message        { get; set; } = string.Empty;

    public Project Project { get; set; } = null!;
}
