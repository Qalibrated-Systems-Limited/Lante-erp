namespace FinanceService.Core.DTOs;

public class CreateSupplierInvoiceDto
{
    public string SupplierId { get; set; } = string.Empty;
    public string SupplierInvoiceNo { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; }
    public DateTime? DueDate { get; set; }
    public string? CurrencyCode { get; set; }
    public string? LpoReference { get; set; }
    public string? Notes { get; set; }
    public List<CreateSupplierInvoiceLineDto> Lines { get; set; } = new();
}

public class CreateSupplierInvoiceLineDto
{
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; } = 1m;
    public decimal UnitPrice { get; set; }
    public string? TaxCode { get; set; }               // A/B/C/E
    public string? ExpenseAccountCode { get; set; }    // defaults 5100
}

public class SupplierInvoiceLineReadDto
{
    public int LineNo { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public string? TaxCode { get; set; }
    public string ExpenseAccountCode { get; set; } = string.Empty;
    public decimal LineSubtotal { get; set; }
    public decimal VatAmount { get; set; }
    public decimal LineTotal { get; set; }
}

public class SupplierInvoiceReadDto
{
    public string Id { get; set; } = string.Empty;
    public string InternalNo { get; set; } = string.Empty;
    public string SupplierInvoiceNo { get; set; } = string.Empty;
    public string SupplierId { get; set; } = string.Empty;
    public string? SupplierName { get; set; }
    public DateTime InvoiceDate { get; set; }
    public DateTime DueDate { get; set; }
    public string Status { get; set; } = string.Empty;
    // #295 (part of #288's remainder): mirrors InvoiceReadDto.CurrencyCode — never carried the
    // bill's own currency back out, even though CreateSupplierInvoiceDto accepts one and the
    // entity persists CurrencyId, so a USD bill displayed and would export as "Kshs" too.
    public string? CurrencyCode { get; set; }
    public string MatchStatus { get; set; } = string.Empty;

    /// <summary>Set when the match was flipped to Exception after the bill was already
    /// Approved/PartPaid (#229) — null on any bill with no unresolved post-approval exception.</summary>
    public DateTime? MatchExceptionFlaggedAt { get; set; }
    public string? LpoReference { get; set; }
    public decimal Subtotal { get; set; }
    public decimal VatAmount { get; set; }
    public decimal Total { get; set; }

    /// <summary>Added with cancellation (#332): the AP read DTO exposed Balance but not PaidAmount,
    /// while the AR one exposes both. Cancelling refuses when PaidAmount is non-zero, and a caller that
    /// cannot see the field has no way to understand the refusal.</summary>
    public decimal PaidAmount { get; set; }

    public decimal Balance { get; set; }
    public string? JournalEntryId { get; set; }
    public string? Notes { get; set; }

    /// <summary>Why the bill was cancelled, and when. Null on every bill that is not cancelled.</summary>
    public string? CancellationReason { get; set; }
    public DateTime? CancelledAt { get; set; }

    public List<SupplierInvoiceLineReadDto> Lines { get; set; } = new();
}

/// <summary>Procurement's 3-way match write-back (Procurement owns the match; Finance stores the result).</summary>
public class SetMatchStatusDto
{
    /// <summary>NotRequired | Pending | Matched | Exception</summary>
    public string Status { get; set; } = string.Empty;
    public string? Note { get; set; }
}

public class CreateVoucherDto
{
    public string? SupplierInvoiceId { get; set; }     // null = ad-hoc voucher
    public string? Payee { get; set; }                 // required for ad-hoc
    public decimal? Amount { get; set; }               // defaults to the invoice balance
    public string? BankAccountCode { get; set; }       // defaults 1100
}

public class ApprovalLogDto
{
    public int StepNumber { get; set; }
    public string? ApproverId { get; set; }
    public string? ApproverRole { get; set; }
    public string Action { get; set; } = string.Empty;
    public DateTime ActionedAt { get; set; }
}

public class VoucherReadDto
{
    public string Id { get; set; } = string.Empty;
    public string VoucherNo { get; set; } = string.Empty;
    public string? SupplierInvoiceId { get; set; }
    public string Payee { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? CurrencyCode { get; set; }
    public string RequiredAuthority { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? JournalEntryId { get; set; }
    public DateTime? PaidAt { get; set; }
    public List<ApprovalLogDto> Approvals { get; set; } = new();
}

/// <summary>Body for POST supplier-invoices/{id}/cancel. A reason is required, not optional.</summary>
public class CancelSupplierInvoiceDto
{
    public string Reason { get; set; } = string.Empty;
}
