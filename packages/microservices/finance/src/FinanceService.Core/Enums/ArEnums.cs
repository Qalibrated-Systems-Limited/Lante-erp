namespace FinanceService.Core.Enums;

/// <summary>
/// Invoice lifecycle.
///
/// <para><b>The values are explicit and must stay that way.</b> EF persists this as an
/// <c>integer</c> column (see TenantFinanceDbContextModelSnapshot), so the ordinals are a stored
/// wire format, not an implementation detail. They were implicit until #343, which is what made
/// removing Overdue dangerous: deleting a member from the middle silently renumbers everything
/// after it, so every row already written as 5 (Cancelled) would have come back as an undefined
/// value — dropping out of the <c>Status != Cancelled</c> filters in AgingAsync, VatService and
/// CashFlowService and quietly reappearing as a live receivable. #346 made Cancelled reachable, so
/// those rows exist.</para>
///
/// <para>4 is deliberately unused. It was Overdue, which nothing ever assigned — grepped for
/// <c>= InvoiceStatus.Overdue</c> and found zero sites. Aging is computed from DueDate against the
/// date being asked about (InvoiceService.AgingAsync), which is always accurate, where a stored flag
/// is only as fresh as the last sweep; finance's one background service runs monthly, so a monthly
/// sweep maintaining a daily-granularity flag would be wrong most of the time. Worse, ReceiptService
/// sets status from the balance on every allocation, so an Overdue invoice taking a part payment
/// would silently stop being overdue while still being overdue. The member was an invitation to
/// implement that. SupplierInvoiceStatus has no equivalent, which is the shape AR should have had.
/// See #343.</para>
/// </summary>
public enum InvoiceStatus
{
    Draft = 0,
    Issued = 1,
    PartPaid = 2,
    Paid = 3,
    // 4 was Overdue — never assigned, removed in #343. Do not reuse: rows may exist with this value
    // only if something assigned it, and nothing ever did, but leaving the gap costs nothing and
    // reusing it would make any such row mean something new.
    Cancelled = 5,
}

public enum PaymentChannel { Bank, Mpesa, Cash, Cheque, Other }

/// KRA eTIMS submission state for a tax invoice.
public enum EtimsStatus { NotSubmitted, Pending, Accepted, Rejected }

public enum VatReturnStatus { Draft, Filed }
