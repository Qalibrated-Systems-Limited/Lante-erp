namespace CrmService.Core.DTOs.Payments;

public class PaymentAlertDto
{
    public string Id { get; set; } = string.Empty;
    public string AlertType { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string TargetRole { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public string? ReferenceType { get; set; }
    public string? ReferenceId { get; set; }
    public string? ReferenceNumber { get; set; }
    public decimal? Amount { get; set; }
    public int? DaysMetric { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? AcknowledgedBy { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class DebtorBucketsDto
{
    public decimal Current { get; set; }
    public decimal Days1To30 { get; set; }
    public decimal Days31To60 { get; set; }
    public decimal Days61Plus { get; set; }
    public decimal Total { get; set; }
}

public class TopDebtorDto
{
    public string CustomerId { get; set; } = string.Empty;
    public string? CustomerName { get; set; }
    public decimal Outstanding { get; set; }
    public decimal Over60 { get; set; }
}

/// <summary>Live payment/debtor snapshot for the current tenant — figures come straight from Finance;
/// counts come from both Finance (invoices/vouchers) and the logged alerts.</summary>
public class PaymentAlertSummaryDto
{
    public bool FinanceConnected { get; set; }
    public decimal OverdueReceivables { get; set; }
    public int OverdueInvoiceCount { get; set; }
    public decimal PayablesDueSoon { get; set; }        // supplier invoices due ≤7d
    public int PayablesDueSoonCount { get; set; }
    public int VouchersAwaitingAuth { get; set; }
    public DebtorBucketsDto Debtors { get; set; } = new();
    public List<TopDebtorDto> TopDebtors { get; set; } = new();
    public int OpenAlerts { get; set; }
    public int CriticalAlerts { get; set; }
}

public record PaymentAlertActionResult(string Status, string Message);
