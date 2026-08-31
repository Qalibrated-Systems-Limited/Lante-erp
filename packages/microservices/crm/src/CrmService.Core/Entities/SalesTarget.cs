using CrmService.Core.Enums;

namespace CrmService.Core.Entities;

/// <summary>P9 — SALES_TARGET (CRM-023/024). Revenue (and activity) target per SE per period, loaded
/// at year start; drives attainment % and RAG status.</summary>
public class SalesTarget : BaseEntity
{
    public string EmployeeId { get; set; } = string.Empty;
    public string? EmployeeName { get; set; }
    public TargetPeriodType PeriodType { get; set; } = TargetPeriodType.Annual;
    public string PeriodLabel { get; set; } = string.Empty;   // e.g. "2026", "2026-Q3", "2026-07"
    public decimal RevenueTarget { get; set; }
    public string Currency { get; set; } = "KES";
}
