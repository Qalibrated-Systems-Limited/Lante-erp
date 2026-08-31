using FinanceService.Core.Enums;

namespace FinanceService.Core.Entities;

public class Supplier : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? KraPin { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public bool IsActive { get; set; } = true;
}

/// Process 11 — supplier invoice (payable). Approval posts Dr expense + Dr input VAT / Cr payables.
/// The 3-way match (PO→GRN→Invoice) is performed in Procurement; we record its result here.
public class SupplierInvoice : BaseEntity
{
    public string InternalNo { get; set; } = string.Empty;      // BILL-2026-0001
    public string SupplierInvoiceNo { get; set; } = string.Empty; // the supplier's own number
    public string SupplierId { get; set; } = string.Empty;
    public string? SupplierName { get; set; }
    public DateTime InvoiceDate { get; set; }
    public DateTime DueDate { get; set; }
    public string CurrencyId { get; set; } = string.Empty;
    public SupplierInvoiceStatus Status { get; set; } = SupplierInvoiceStatus.Received;
    public ThreeWayMatchStatus MatchStatus { get; set; } = ThreeWayMatchStatus.NotRequired;
    public string? LpoReference { get; set; }

    /// <summary>Set when the match is flipped to <see cref="ThreeWayMatchStatus.Exception"/> after the
    /// bill was already Approved (liability already posted) or PartPaid — the GL says "we owe this"
    /// while the match now says it shouldn't have been approved, and nothing else surfaces that
    /// contradiction (#229). Cleared when the match moves off Exception. While set, this bill is
    /// refused for a new payment voucher (see <c>PaymentVoucherService.CreateAsync</c>).</summary>
    public DateTime? MatchExceptionFlaggedAt { get; set; }

    public decimal Subtotal { get; set; }
    public decimal VatAmount { get; set; }        // input VAT (recoverable)
    public decimal Total { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal Balance { get; set; }

    public string PayablesAccountCode { get; set; } = "2100";
    public string? JournalEntryId { get; set; }
    public string? Notes { get; set; }

    /// <summary>Why this bill was cancelled. Required when cancelling, and separate from
    /// <see cref="Notes"/> for the same reason as on Invoice: Notes is user-editable free text that a
    /// later edit would overwrite, and "why did this expense disappear" must stay answerable.</summary>
    public string? CancellationReason { get; set; }
    public DateTime? CancelledAt { get; set; }

    public ICollection<SupplierInvoiceLine> Lines { get; set; } = new List<SupplierInvoiceLine>();
}

public class SupplierInvoiceLine : BaseEntity
{
    public string SupplierInvoiceId { get; set; } = string.Empty;
    public int LineNo { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; } = 1m;
    public decimal UnitPrice { get; set; }
    public string TaxCategoryId { get; set; } = string.Empty;
    public string ExpenseAccountCode { get; set; } = "5100";
    public decimal LineSubtotal { get; set; }
    public decimal VatAmount { get; set; }
    public decimal LineTotal { get; set; }
}

/// Process 12 — the payment authority matrix: which role must approve a payment of a given size.
public class PaymentApprovalTier : BaseEntity
{
    public decimal MinAmount { get; set; }
    public decimal? MaxAmount { get; set; }        // null = no upper limit (MD)
    public string RequiredRole { get; set; } = string.Empty;
    public int StepNumber { get; set; }
}

/// A payment voucher raised against a supplier invoice (or ad-hoc). Approval authority is set by amount.
public class PaymentVoucher : BaseEntity
{
    public string VoucherNo { get; set; } = string.Empty;   // PV-2026-0001
    public string? SupplierInvoiceId { get; set; }
    public string Payee { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string CurrencyId { get; set; } = string.Empty;
    public string RequiredAuthority { get; set; } = string.Empty;   // role from the matrix
    public VoucherStatus Status { get; set; } = VoucherStatus.PendingApproval;
    public string PayablesAccountCode { get; set; } = "2100";
    public string BankAccountCode { get; set; } = "1100";
    public string? JournalEntryId { get; set; }
    public DateTime? PaidAt { get; set; }

    public ICollection<PaymentApprovalLog> Approvals { get; set; } = new List<PaymentApprovalLog>();
}

public class PaymentApprovalLog : BaseEntity
{
    public string VoucherId { get; set; } = string.Empty;
    public int StepNumber { get; set; }
    public string? ApproverId { get; set; }
    public string? ApproverRole { get; set; }
    public ApprovalAction Action { get; set; }
    public DateTime ActionedAt { get; set; } = DateTime.UtcNow;
    public string? Notes { get; set; }
}
