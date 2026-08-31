using OperationsService.Core.Enums;

namespace OperationsService.Core.Entities;

public class PerformanceMetrics : BaseEntity
{
    public string TechnicianId { get; set; } = string.Empty;
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public int TotalAssignments { get; set; }
    public int CompletedAssignments { get; set; }
    public int DelayedAssignments { get; set; }
    public double CompletionRate { get; set; }
    public double OnTimeRate { get; set; }
    public double PerformanceScore { get; set; }
    public AlertLevel AlertLevel { get; set; } = AlertLevel.None;
    public double AverageCompletionMinutes { get; set; }
    public double TotalWorkingHours { get; set; }
    public int ReportsSubmitted { get; set; }
    public int ReportsApproved { get; set; }
    public int ReportsRejected { get; set; }
    public double ReportApprovalRate { get; set; }
    public int TotalRequisitions { get; set; }
    public int ApprovedRequisitions { get; set; }
    public decimal TotalRequisitionAmount { get; set; }
    public double AmberThreshold { get; set; } = 70;
    public double RedThreshold { get; set; } = 50;
}
