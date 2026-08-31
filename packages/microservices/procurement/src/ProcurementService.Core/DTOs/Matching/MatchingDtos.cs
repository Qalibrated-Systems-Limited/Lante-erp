namespace ProcurementService.Core.DTOs.Matching;

// ── Reads ──
public class MatchExceptionDto
{
    public string Id { get; set; } = string.Empty;
    public string ThreeWayMatchId { get; set; } = string.Empty;
    public string PoId { get; set; } = string.Empty;
    public string? PoNumber { get; set; }
    public string ExceptionType { get; set; } = string.Empty;
    public string? Detail { get; set; }
    public string Status { get; set; } = string.Empty;
    public string RaisedBy { get; set; } = string.Empty;
    public DateTime RaisedAt { get; set; }
    public string? ResolvedBy { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? Resolution { get; set; }
}

public class MatchReadDto
{
    public string Id { get; set; } = string.Empty;
    public string PoId { get; set; } = string.Empty;
    public string PoNumber { get; set; } = string.Empty;
    public string? SupplierName { get; set; }

    public string? SupplierInvoiceId { get; set; }
    public string? InvoiceNumber { get; set; }

    public decimal PoTotal { get; set; }
    public decimal InvoiceTotal { get; set; }
    public decimal ReceivedQty { get; set; }

    /// <summary>Goods-receipt state carried over from the LPO (pushed by the Stores GRN callback, P5).</summary>
    public string ReceiptStatus { get; set; } = "NotReceived";

    public bool ReceivedOk { get; set; }
    public bool PriceOk { get; set; }
    public bool TotalOk { get; set; }

    public string Status { get; set; } = string.Empty;
    public string? PaymentVoucherRef { get; set; }
    public string? PaymentVoucherNo { get; set; }
    public string? MatchedBy { get; set; }
    public DateTime? MatchedAt { get; set; }
    public DateTime CreatedAt { get; set; }

    public List<MatchExceptionDto> Exceptions { get; set; } = new();
    /// <summary>True when the match is clean, unresolved-exception-free and no voucher has been raised yet.</summary>
    public bool CanRaiseVoucher { get; set; }
}

public class MatchRowDto
{
    public string Id { get; set; } = string.Empty;
    public string PoId { get; set; } = string.Empty;
    public string PoNumber { get; set; } = string.Empty;
    public string? SupplierName { get; set; }
    public decimal PoTotal { get; set; }
    public decimal InvoiceTotal { get; set; }
    public string? InvoiceNumber { get; set; }
    public string Status { get; set; } = string.Empty;
    public int OpenExceptions { get; set; }
    public string? PaymentVoucherNo { get; set; }
    public DateTime? MatchedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class MatchFilterParams
{
    public string? Status { get; set; }
    public string? PoId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public record MatchListResult(List<MatchRowDto> Items, int Total);

public class MatchSummaryDto
{
    public int Total { get; set; }
    public int Matched { get; set; }
    public int WithExceptions { get; set; }
    public int OpenExceptions { get; set; }
    /// <summary>Clean matches still waiting for a payment voucher to be handed to Finance.</summary>
    public int AwaitingVoucher { get; set; }
    public int VouchersRaised { get; set; }
    public decimal MatchedValue { get; set; }
    /// <summary>Issued LPOs that have never been run through the matching engine.</summary>
    public int UnmatchedLpos { get; set; }
}

// ── Actions ──
public record MatchActionResult(string Status, string Message, string? MatchId = null);

public class ResolveExceptionDto
{
    /// <summary>Mandatory — how the discrepancy was settled (credit note, short-delivery accepted, …).</summary>
    public string Resolution { get; set; } = string.Empty;
}

public class RaiseVoucherDto
{
    /// <summary>Optional override; Finance defaults the voucher to the supplier-invoice balance.</summary>
    public decimal? Amount { get; set; }
    public string? BankAccountCode { get; set; }
}
