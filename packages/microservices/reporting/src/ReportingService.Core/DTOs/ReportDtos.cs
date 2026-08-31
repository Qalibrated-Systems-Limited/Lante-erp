namespace ReportingService.Core.DTOs;

// ---- 1. Management accounts (P&L + trial balance + server-computed balance sheet) ----

public class BalanceSheetRowDto
{
    public string AccountId { get; set; } = string.Empty;
    public string AccountCode { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public decimal Balance { get; set; }
}

public class BalanceSheetDto
{
    public List<BalanceSheetRowDto> Assets { get; set; } = new();
    public List<BalanceSheetRowDto> Liabilities { get; set; } = new();
    public List<BalanceSheetRowDto> Equity { get; set; } = new();
    public decimal TotalAssets { get; set; }
    public decimal TotalLiabilities { get; set; }
    public decimal TotalEquity { get; set; }
    public decimal CurrentYearProfit { get; set; }
    public bool IsBalanced { get; set; }
}

public class ManagementAccountsReportDto
{
    public string? PeriodId { get; set; }
    public DateTime AsOf { get; set; }
    public ProfitLossDto? ProfitAndLoss { get; set; }
    public TrialBalanceDto? TrialBalance { get; set; }
    public BalanceSheetDto? BalanceSheet { get; set; }
    public List<string> Warnings { get; set; } = new();
}

// ---- 2. Budget vs actual variance ----

public class BudgetVarianceReportDto
{
    public string FiscalYearId { get; set; } = string.Empty;
    public List<BudgetReadDto> Budgets { get; set; } = new();
    public List<RevenueTargetReadDto> RevenueTargets { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

// ---- 5. Project profitability ----

public class ProjectProfitabilityRowDto
{
    public string ProjectId { get; set; } = string.Empty;
    public string? ProjectName { get; set; }
    public decimal PlannedBudget { get; set; }
    public decimal TotalEstimated { get; set; }
    public decimal ActualCost { get; set; }
    public decimal Remaining { get; set; }
    public decimal UtilizationPercent { get; set; }
}

public class ProjectProfitabilityReportDto
{
    public List<ProjectProfitabilityRowDto> Rows { get; set; } = new();
    public ProjectProfitabilityRowDto Totals { get; set; } = new() { ProjectId = "TOTAL", ProjectName = "Portfolio Total" };
    public List<string> Warnings { get; set; } = new();
}

// ---- 6. Fleet cost & utilisation ----

public class FleetTruckCostRowDto
{
    public string TruckId { get; set; } = string.Empty;
    public string? LicensePlate { get; set; }
    public string? Model { get; set; }
    public int TripCount { get; set; }
    public decimal TotalCost { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal TotalProfit { get; set; }
    public decimal TotalMileage { get; set; }
}

public class FleetCostUtilisationReportDto
{
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public List<FleetTruckCostRowDto> Trucks { get; set; } = new();
    public FleetTruckCostRowDto Totals { get; set; } = new() { TruckId = "TOTAL" };
    public List<string> Warnings { get; set; } = new();
}

// ---- 7. Procurement spend ----

public class ProcurementSpendBySupplierDto
{
    public string SupplierId { get; set; } = string.Empty;
    public string? SupplierName { get; set; }
    public decimal TotalLandedCost { get; set; }
    public int LineCount { get; set; }
}

public class ProcurementSpendByCategoryDto
{
    public string? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public decimal TotalLandedCost { get; set; }
    public int LineCount { get; set; }
}

public class ProcurementSpendReportDto
{
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public decimal GrandTotal { get; set; }
    public List<ProcurementSpendBySupplierDto> BySupplier { get; set; } = new();
    public List<ProcurementSpendByCategoryDto> ByCategory { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

// ---- 8. HSE incidents & TRIR ----

public class HseIncidentsTrirReportDto
{
    public HseDashboardDto? Dashboard { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public List<HseIncidentReadDto> Incidents { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

// ---- 9. Compliance dashboard ----

public class ComplianceDashboardReportDto
{
    public ComplianceDashboardDto? ComplianceDashboard { get; set; }
    public StatutoryDashboardDto? StatutoryDashboard { get; set; }
    public List<PolicyReadDto> Policies { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}
