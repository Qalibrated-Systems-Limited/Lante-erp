using FinanceService.Core.Enums;

namespace FinanceService.Core.Entities;

/// A client the company invoices. IsGovernment routes to the Government receivables account (1201).
public class Customer : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? KraPin { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public decimal CreditLimit { get; set; }
    public bool IsGovernment { get; set; }
    public bool IsActive { get; set; } = true;
}

/// VAT category (KRA): A standard 16%, B/C zero-rated, E exempt. Seeded.
public class TaxCategory : BaseEntity
{
    public string Code { get; set; } = string.Empty;   // A/B/C/E
    public string Name { get; set; } = string.Empty;
    public decimal Rate { get; set; }                   // 0.16 / 0
    public bool IsActive { get; set; } = true;
}

/// Process 15 — customer invoice. Issuing posts to the GL and submits to eTIMS.
public class Invoice : BaseEntity
{
    public string InvoiceNo { get; set; } = string.Empty;   // INV-2026-0001
    public string CustomerId { get; set; } = string.Empty;
    public string? CustomerName { get; set; }
    public DateTime InvoiceDate { get; set; }
    public DateTime DueDate { get; set; }                    // 30-day terms (FIN-010)
    public string CurrencyId { get; set; } = string.Empty;
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;

    public decimal Subtotal { get; set; }
    public decimal VatAmount { get; set; }
    public decimal Total { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal Balance { get; set; }

    public string ReceivableAccountCode { get; set; } = "1200";
    public string? MilestoneId { get; set; }                 // FIN-013 link to project milestone
    public string? SourceModule { get; set; }                // e.g. CRM deal, Projects
    public string? SourceDocumentId { get; set; }
    public string? JournalEntryId { get; set; }

    public string? EtimsReference { get; set; }
    public EtimsStatus EtimsStatus { get; set; } = EtimsStatus.NotSubmitted;
    public string? Notes { get; set; }

    /// <summary>Why this invoice was cancelled. Required when cancelling, and kept separate from
    /// <see cref="Notes"/> because Notes is user-editable free text that a later edit would overwrite —
    /// "why did this invoice disappear from revenue" must stay answerable.</summary>
    public string? CancellationReason { get; set; }

    /// <summary>When it was cancelled. UpdatedAt is not a substitute: it moves on any later write, and
    /// the reversing journal it triggers is dated by its own period, not by this.</summary>
    public DateTime? CancelledAt { get; set; }

    public ICollection<InvoiceLine> Lines { get; set; } = new List<InvoiceLine>();
}

public class InvoiceLine : BaseEntity
{
    public string InvoiceId { get; set; } = string.Empty;
    public int LineNo { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; } = 1m;
    public decimal UnitPrice { get; set; }
    public string TaxCategoryId { get; set; } = string.Empty;
    public string RevenueAccountCode { get; set; } = "4100";
    public decimal LineSubtotal { get; set; }
    public decimal VatAmount { get; set; }
    public decimal LineTotal { get; set; }
}

/// Process 16 — a receipt/payment from a customer, allocated across one or more invoices.
public class Payment : BaseEntity
{
    public string PaymentNo { get; set; } = string.Empty;   // RCP-2026-0001
    public string CustomerId { get; set; } = string.Empty;
    public string? CustomerName { get; set; }
    public DateTime PaymentDate { get; set; }
    public decimal Amount { get; set; }
    public string CurrencyId { get; set; } = string.Empty;
    public PaymentChannel Channel { get; set; } = PaymentChannel.Bank;
    public string? ReceiptReference { get; set; }
    public string? ReceivedBy { get; set; }
    public string DepositAccountCode { get; set; } = "1100";
    public string? JournalEntryId { get; set; }
    public decimal UnallocatedAmount { get; set; }

    public ICollection<PaymentAllocation> Allocations { get; set; } = new List<PaymentAllocation>();
}

public class PaymentAllocation : BaseEntity
{
    public string PaymentId { get; set; } = string.Empty;
    public string InvoiceId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

/// Process 18 — a filed VAT return for a period (output − input = net payable).
public class VatReturn : BaseEntity
{
    public string PeriodId { get; set; } = string.Empty;
    public decimal OutputVat { get; set; }
    public decimal InputVat { get; set; }
    public decimal NetVatPayable { get; set; }
    public VatReturnStatus Status { get; set; } = VatReturnStatus.Draft;
    public DateTime? FiledAt { get; set; }
    public string? FiledBy { get; set; }
}

/// Record of an eTIMS submission for an invoice (or credit/debit note).
public class EtimsSubmission : BaseEntity
{
    public string InvoiceId { get; set; } = string.Empty;
    public string SubmissionType { get; set; } = "Invoice";
    public string? EtimsReference { get; set; }
    public EtimsStatus Status { get; set; } = EtimsStatus.Pending;
    public DateTime? AcknowledgedAt { get; set; }
}
