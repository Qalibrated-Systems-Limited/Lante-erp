namespace ProcurementService.Core.Enums;

/// <summary>P6 — outcome of the 3-way match (PO vs GRN vs supplier invoice). A clean match generates a
/// payment voucher; any failed check raises exception(s) for Finance-Manager resolution.</summary>
public enum MatchStatus
{
    Pending,
    Matched,
    Exception,
}

/// <summary>P6 — the specific check that failed in the 3-way match.</summary>
public enum MatchExceptionType
{
    NotFullyReceived,   // GRN accepted qty does not satisfy the PO
    NoInvoice,          // no supplier invoice linked to the LPO in Finance
    PriceMismatch,      // invoice total != PO total (outside tolerance)
    TotalExceedsPo,     // invoice total > PO total
}

/// <summary>P6 — matching-exception lifecycle.</summary>
public enum MatchExceptionStatus
{
    Open,
    Resolved,
}
