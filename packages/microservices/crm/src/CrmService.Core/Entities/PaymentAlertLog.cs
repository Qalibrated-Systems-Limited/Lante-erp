using CrmService.Core.Enums;

namespace CrmService.Core.Entities;

/// <summary>P14 (CRM-063) — PAYMENT_ALERT_LOG. The only C13 table: a log of payment/debtor alerts the
/// background worker raises from Finance data (AR invoices, supplier invoices, vouchers, debtor aging).
/// <see cref="DedupKey"/> makes each logical alert idempotent so the 5-minute sweep never duplicates it.</summary>
public class PaymentAlertLog : BaseEntity
{
    public PaymentAlertType AlertType { get; set; }
    public PaymentAlertSeverity Severity { get; set; } = PaymentAlertSeverity.Warning;
    public string TargetRole { get; set; } = string.Empty;    // who the alert is for (e.g. "Finance Manager", "CFO", "MD")
    public string Message { get; set; } = string.Empty;

    // Optional links back to the Finance source record (string refs — no cross-service FK).
    public string? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public string? ReferenceType { get; set; }                // Invoice / SupplierInvoice / Voucher / Debtor / Summary
    public string? ReferenceId { get; set; }
    public string? ReferenceNumber { get; set; }
    public decimal? Amount { get; set; }
    public int? DaysMetric { get; set; }                      // e.g. days overdue / days to due

    public string DedupKey { get; set; } = string.Empty;      // idempotency key for the sweep
    public PaymentAlertStatus Status { get; set; } = PaymentAlertStatus.Open;
    public string? AcknowledgedBy { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
}
