namespace ReportingService.Core.DTOs;

// Mirrors FinanceService.Core.DTOs shapes returned by GET /api/v1/finance/... endpoints.
// FinanceService's controllers are only gated by a bare [Authorize] (no named policy) — see
// BaseFinanceController — so no permission-policy translation is needed on this side.

public class FiscalYearDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class PnlLineDto
{
    public string AccountCode { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

public class PnlDepartmentDto
{
    public string CostCentre { get; set; } = string.Empty;
    public decimal Income { get; set; }
    public decimal Expense { get; set; }
    public decimal Net { get; set; }
}

public class ProfitLossDto
{
    public string PeriodId { get; set; } = string.Empty;
    public string PeriodName { get; set; } = string.Empty;
    public decimal IncomeTotal { get; set; }
    public decimal ExpenseTotal { get; set; }
    public decimal NetProfit { get; set; }
    public List<PnlLineDto> Income { get; set; } = new();
    public List<PnlLineDto> Expenses { get; set; } = new();
    public List<PnlDepartmentDto> ByDepartment { get; set; } = new();
}

public class TrialBalanceRowDto
{
    public string AccountId { get; set; } = string.Empty;
    public string AccountCode { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string Classification { get; set; } = string.Empty;
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
}

public class TrialBalanceDto
{
    public DateTime AsOf { get; set; }
    public List<TrialBalanceRowDto> Rows { get; set; } = new();
    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }
    public bool IsBalanced { get; set; }
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
    public string Status { get; set; } = string.Empty;
}

public class RevenueTargetReadDto
{
    public string Id { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
    public decimal AnnualAmount { get; set; }
    public decimal Actual { get; set; }
    public decimal Variance { get; set; }
    public double AchievedPct { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class DebtorAgingRowDto
{
    public string CustomerId { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public decimal Current { get; set; }
    public decimal Days1To30 { get; set; }
    public decimal Days31To60 { get; set; }
    public decimal Days61Plus { get; set; }
    public decimal Total { get; set; }
}

public class CashFlowWeekDto
{
    public string Label { get; set; } = string.Empty;
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public decimal ExpectedIn { get; set; }
    public decimal ExpectedOut { get; set; }
    public decimal OfWhichPayroll { get; set; }
    public decimal Net { get; set; }
    public decimal ProjectedBalance { get; set; }
}

public class CashFlowDto
{
    public DateTime AsOf { get; set; }
    public decimal CashNow { get; set; }
    public decimal OverdueReceivables { get; set; }
    public decimal OverduePayables { get; set; }
    public decimal LowestProjectedBalance { get; set; }
    public List<CashFlowWeekDto> Weeks { get; set; } = new();
}
