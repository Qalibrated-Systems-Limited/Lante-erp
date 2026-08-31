namespace CrmService.Core.Entities;

/// <summary>P7 — VISIT_TARGET. Configurable minimum client-visit targets per SE (underperformance
/// is flagged to Head of BD).</summary>
public class VisitTarget : BaseEntity
{
    public string EmployeeId { get; set; } = string.Empty;
    public string? EmployeeName { get; set; }
    public int MinVisitsPerWeek { get; set; }
    public int MinVisitsPerMonth { get; set; }
}
