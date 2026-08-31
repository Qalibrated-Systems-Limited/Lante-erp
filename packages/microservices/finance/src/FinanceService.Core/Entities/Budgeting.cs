namespace FinanceService.Core.Entities;

/// Process 17 — an annual department/cost-centre budget. Actual is computed live from the posted
/// GL (expense accounts), so only the plan is stored here. FIN-020/021: amber @80%, red @100%.
public class Budget : BaseEntity
{
    public string FiscalYearId { get; set; } = string.Empty;
    public string? CostCenterId { get; set; }          // null = company-wide
    public string DepartmentName { get; set; } = string.Empty;
    public string? CostCentreLabel { get; set; }
    public decimal AnnualAmount { get; set; }
    public string BudgetType { get; set; } = "Annual"; // Annual / Revised
    public int Version { get; set; } = 1;
    public string? ApprovedBy { get; set; }
    public bool IsActive { get; set; } = true;
}

/// A company or departmental revenue target for the year; actual = posted income from the GL.
public class RevenueTarget : BaseEntity
{
    public string FiscalYearId { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;  // "Company-wide" or a department/business line
    public string? CostCenterId { get; set; }
    public decimal AnnualAmount { get; set; }
}
