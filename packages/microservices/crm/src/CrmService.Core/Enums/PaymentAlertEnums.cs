namespace CrmService.Core.Enums;

// C13 (P14) — payment / debtor alerts. No new financial transaction tables; reads Finance
// INVOICE / SUPPLIER_INVOICE / VOUCHER and logs alerts here.

public enum PaymentAlertType
{
    SupplierInvoiceDue,     // AP invoice due within 7 days
    ClientInvoiceOverdue,   // AR invoice past due
    SupplierUnauthorised,   // voucher awaiting authorisation 3+ days (FM + CFO red flag)
    DebtorOver45,           // AR invoice >45 days overdue → Account Owner + Line Manager
    DebtorOver60,           // AR invoice >60 days overdue → MD
    WeeklyCfoSummary,       // weekly supplier + debtor summary → CFO
    MonthlyMdDashboard,     // monthly priority dashboard → MD
}

public enum PaymentAlertSeverity { Info, Warning, Critical }
public enum PaymentAlertStatus { Open, Acknowledged }
