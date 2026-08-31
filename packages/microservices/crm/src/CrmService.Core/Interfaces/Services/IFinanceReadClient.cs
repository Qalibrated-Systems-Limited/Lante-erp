namespace CrmService.Core.Interfaces.Services;

// C13 (P14) — read-only projections of the Finance service's AR/AP data that the payment-alert
// engine consumes. Kept deliberately small: only the fields the alerts need. Populated by the real
// Finance HTTP client (per-tenant-schema), or empty when Finance is not configured/reachable.

public record FinanceArInvoice(string Id, string InvoiceNo, string? CustomerId, string? CustomerName,
    DateTime InvoiceDate, DateTime DueDate, string Status, decimal Total, decimal PaidAmount, decimal Balance);

public record FinanceDebtorAging(string CustomerId, string? CustomerName,
    decimal Current, decimal Days1To30, decimal Days31To60, decimal Days61Plus, decimal Total);

public record FinanceSupplierInvoice(string Id, string InternalNo, string SupplierInvoiceNo, string? SupplierId,
    string? SupplierName, DateTime InvoiceDate, DateTime DueDate, string Status, decimal Total, decimal Balance);

public record FinanceVoucher(string Id, string VoucherNo, string? SupplierInvoiceId, string? Payee,
    decimal Amount, string? RequiredAuthority, string Status, DateTime? PaidAt);

/// <summary>Reads the Finance service over HTTP, scoped to a tenant schema. All methods degrade to an
/// empty list on any failure / when Finance is not configured, so the alert sweep never throws.</summary>
public interface IFinanceReadClient
{
    bool IsConfigured { get; }
    Task<List<FinanceArInvoice>> GetArInvoicesAsync(string schema, CancellationToken ct = default);
    Task<List<FinanceDebtorAging>> GetDebtorAgingAsync(string schema, CancellationToken ct = default);
    Task<List<FinanceSupplierInvoice>> GetSupplierInvoicesAsync(string schema, CancellationToken ct = default);
    Task<List<FinanceVoucher>> GetVouchersAsync(string schema, CancellationToken ct = default);
}
