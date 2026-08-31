using OperationsService.Core.Enums;

namespace OperationsService.Core.DTOs.Performance;

/// <summary>
/// #339 -- rewritten to actually match <see cref="Entities.PerformanceMetrics"/>. The original shape
/// (CancelledAssignments, ServiceReportsSubmitted/Approved, TotalExpensesClaimed/Approved,
/// AverageCompletionHours) shared only five field names with the entity and had no source for the
/// rest -- ComputeMetricsAsync could not have populated it without inventing data. TechnicianName,
/// DepartmentId, Month and Year are the only genuinely derived fields (the entity stores PeriodStart/
/// PeriodEnd and neither a technician's display name nor their department, both resolved from
/// Assignment/AssignedTechnician at read time); everything else is a 1:1 property of the entity.
/// </summary>
public class PerformanceMetricsReadDto
{
    public string Id { get; set; } = string.Empty;
    public string TechnicianId { get; set; } = string.Empty;
    public string TechnicianName { get; set; } = string.Empty;
    public string DepartmentId { get; set; } = string.Empty;
    public int Month { get; set; }
    public int Year { get; set; }
    public int TotalAssignments { get; set; }
    public int CompletedAssignments { get; set; }
    public int DelayedAssignments { get; set; }
    public double CompletionRate { get; set; }
    public double OnTimeRate { get; set; }
    public double PerformanceScore { get; set; }
    public AlertLevel AlertLevel { get; set; }
    public double AverageCompletionMinutes { get; set; }
    public double TotalWorkingHours { get; set; }
    public int ReportsSubmitted { get; set; }
    public int ReportsApproved { get; set; }
    public int ReportsRejected { get; set; }
    public double ReportApprovalRate { get; set; }
    public int TotalRequisitions { get; set; }
    public int ApprovedRequisitions { get; set; }
    public decimal TotalRequisitionAmount { get; set; }
    public DateTime ComputedAt { get; set; }
}

public class PerformanceSummaryDto
{
    public string DepartmentId { get; set; } = string.Empty;
    public int Month { get; set; }
    public int Year { get; set; }
    public List<PerformanceMetricsReadDto> TechnicianMetrics { get; set; } = [];
    public int DepartmentTotalAssignments { get; set; }
    public int DepartmentCompletedAssignments { get; set; }
    public double DepartmentCompletionRate { get; set; }
}

public class PerformanceFilterParameters
{
    public string? DepartmentId { get; set; }
    public string? TechnicianId { get; set; }
    public int? Month { get; set; }
    public int? Year { get; set; }
}
