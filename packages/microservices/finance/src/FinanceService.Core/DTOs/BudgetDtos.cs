namespace FinanceService.Core.DTOs;

public class CreateBudgetDto
{
    public string FiscalYearId { get; set; } = string.Empty;
    public string DepartmentName { get; set; } = string.Empty;
    public string? CostCenterId { get; set; }
    public string? CostCentreLabel { get; set; }
    public decimal AnnualAmount { get; set; }
    public string? BudgetType { get; set; }
}

public class BudgetReadDto
{
    public string Id { get; set; } = string.Empty;
    public string DepartmentName { get; set; } = string.Empty;
    public string? CostCentreLabel { get; set; }
    public decimal AnnualAmount { get; set; }
    public decimal Actual { get; set; }
    public decimal Variance { get; set; }
    public double ConsumedPct { get; set; }
    public string Status { get; set; } = string.Empty;   // OnTrack / Warning / Over
    public int Version { get; set; }
    public string BudgetType { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public class CreateRevenueTargetDto
{
    public string FiscalYearId { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
    public string? CostCenterId { get; set; }
    public decimal AnnualAmount { get; set; }
}

public class RevenueTargetReadDto
{
    public string Id { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
    public decimal AnnualAmount { get; set; }
    public decimal Actual { get; set; }
    public decimal Variance { get; set; }
    public double AchievedPct { get; set; }
    public string Status { get; set; } = string.Empty;   // Behind / OnTrack / Achieved
}
