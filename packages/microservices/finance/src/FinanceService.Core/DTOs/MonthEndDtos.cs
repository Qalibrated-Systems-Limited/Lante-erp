namespace FinanceService.Core.DTOs;

public class ChecklistItemDto
{
    public string Id { get; set; } = string.Empty;
    public string Item { get; set; } = string.Empty;
    public bool IsComplete { get; set; }
    public string? CompletedBy { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public class PeriodCloseDto
{
    public string PeriodId { get; set; } = string.Empty;
    public string PeriodName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;   // Open / Closed / Locked
    public List<ChecklistItemDto> Checklist { get; set; } = new();
    public bool AllComplete { get; set; }
    public bool CanClose { get; set; }
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
