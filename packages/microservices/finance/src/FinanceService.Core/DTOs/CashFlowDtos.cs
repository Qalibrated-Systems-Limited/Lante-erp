namespace FinanceService.Core.DTOs;

public class CashFlowWeekDto
{
    public string Label { get; set; } = string.Empty;      // W1…
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
    public decimal CashNow { get; set; }                   // GL bank + cash balance today
    public decimal OverdueReceivables { get; set; }        // past-due invoices (chase — not forecast)
    public decimal OverduePayables { get; set; }           // past-due bills (due now — not forecast)
    public decimal LowestProjectedBalance { get; set; }
    public List<CashFlowWeekDto> Weeks { get; set; } = new();
}
