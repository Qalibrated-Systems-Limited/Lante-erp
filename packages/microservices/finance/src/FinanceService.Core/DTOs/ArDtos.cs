namespace FinanceService.Core.DTOs;

public class CreateInvoiceDto
{
    public string CustomerId { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; }
    public DateTime? DueDate { get; set; }               // defaults to InvoiceDate + 30 days
    public string? CurrencyCode { get; set; }
    public string? MilestoneId { get; set; }
    public string? SourceModule { get; set; }
    public string? SourceDocumentId { get; set; }
    public string? Notes { get; set; }
    public List<CreateInvoiceLineDto> Lines { get; set; } = new();
}

public class CreateInvoiceLineDto
{
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; } = 1m;
    public decimal UnitPrice { get; set; }
    public string? TaxCategoryId { get; set; }
    public string? TaxCode { get; set; }                 // alternative: "A"/"B"/"C"/"E"
    public string? RevenueAccountCode { get; set; }      // defaults 4100
}

public class InvoiceLineReadDto
{
    public int LineNo { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public string? TaxCode { get; set; }
    public string RevenueAccountCode { get; set; } = string.Empty;
    public decimal LineSubtotal { get; set; }
    public decimal VatAmount { get; set; }
    public decimal LineTotal { get; set; }
}

public class InvoiceReadDto
{
    public string Id { get; set; } = string.Empty;
    public string InvoiceNo { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public string? CustomerName { get; set; }
    public DateTime InvoiceDate { get; set; }
    public DateTime DueDate { get; set; }
    public string Status { get; set; } = string.Empty;
    // #288: the frontend rendered every invoice as "Kshs" regardless of what it was actually
    // raised in — this DTO never carried the currency back out, even though CreateInvoiceDto
    // accepts one and the entity persists CurrencyId. Null only for rows created before this
    // field existed and somehow missing a resolvable CurrencyId; the frontend's fmt.money
    // already treats null as the base currency, matching every invoice's actual common case.
    public string? CurrencyCode { get; set; }
    public decimal Subtotal { get; set; }
    public decimal VatAmount { get; set; }
    public decimal Total { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal Balance { get; set; }
    /// <summary>Why the invoice was cancelled, and when. Null on every invoice that is not cancelled.
    /// Carried out to the caller because "cancelled" without a reason is the state that generates the
    /// support ticket.</summary>
    public string? CancellationReason { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? EtimsReference { get; set; }
    public string EtimsStatus { get; set; } = string.Empty;
    public string? JournalEntryId { get; set; }
    public string? Notes { get; set; }
    public List<InvoiceLineReadDto> Lines { get; set; } = new();
}

public class AllocationDto
{
    public string InvoiceId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

public class CreateReceiptDto
{
    public string CustomerId { get; set; } = string.Empty;
    public DateTime PaymentDate { get; set; }
    public decimal Amount { get; set; }
    public string? CurrencyCode { get; set; }
    public string? Channel { get; set; }                 // Bank/Mpesa/Cash/Cheque
    public string? ReceiptReference { get; set; }
    public string? DepositAccountCode { get; set; }      // defaults 1100
    /// Optional explicit allocations; if empty the receipt auto-allocates oldest invoice first.
    public List<AllocationDto> Allocations { get; set; } = new();
}

public class ReceiptReadDto
{
    /// <summary>The currency this record was transacted in. Persisted as CurrencyId on the entity and
    /// projected here because the read side silently dropped it: currency was captured, validated
    /// against a configured rate, stored — and then discarded on the way out, so no consumer could tell
    /// a USD record from a KES one (#288).</summary>
    public string? CurrencyCode { get; set; }
    public string Id { get; set; } = string.Empty;
    public string PaymentNo { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public string? CustomerName { get; set; }
    public DateTime PaymentDate { get; set; }
    public decimal Amount { get; set; }
    public string Channel { get; set; } = string.Empty;
    public string? ReceiptReference { get; set; }
    public decimal UnallocatedAmount { get; set; }
    public string? JournalEntryId { get; set; }
    public List<AllocationDto> Allocations { get; set; } = new();
}

public class DebtorAgingRowDto
{
    public string CustomerId { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public decimal Current { get; set; }        // not yet due
    public decimal Days1To30 { get; set; }
    public decimal Days31To60 { get; set; }
    public decimal Days61Plus { get; set; }
    public decimal Total { get; set; }
}

public class VatReturnDto
{
    public string PeriodId { get; set; } = string.Empty;
    public string PeriodName { get; set; } = string.Empty;
    public decimal OutputVat { get; set; }
    public decimal InputVat { get; set; }
    public decimal NetVatPayable { get; set; }
    public string Status { get; set; } = "Draft";
}

/// <summary>Body for POST invoices/{id}/cancel. A reason is required rather than optional — a
/// cancelled invoice with no stated reason is the case that becomes unanswerable months later.</summary>
public class CancelInvoiceDto
{
    public string Reason { get; set; } = string.Empty;
}
