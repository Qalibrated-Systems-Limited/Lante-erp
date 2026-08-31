namespace ProcurementService.Core.Enums;

/// <summary>P4 — LPO lifecycle. Draft → PendingApproval (routed through the authority matrix, each step
/// digitally signed) → Approved → Issued (emailed to supplier, commitment journal posted). Rejection
/// returns it to Procurement.</summary>
public enum PoStatus
{
    Draft,
    PendingApproval,
    Approved,
    Rejected,
    Issued,
    Cancelled,
}

/// <summary>P4 — approval-authority roles (QSL Financial Policies LPO matrix), by LPO value band.</summary>
public enum ApprovalRole
{
    ProcurementOfficer,   // ≤ 10,000
    ProcurementManager,   // 10,001 – 100,000
    FinanceManager,       // 100,001 – 500,000 (then MD)
    MD,                   // 100,001 – 500,000 (after Finance) and > 500,000 (with Board Resolution)
    Board,
}

/// <summary>P4 — status of a single step in the LPO approval chain.</summary>
public enum ApprovalStepStatus
{
    Pending,
    Approved,
    Rejected,
}

/// <summary>P5 — goods-receipt status of an issued LPO, set by the Stores GRN callback.</summary>
public enum PoReceiptStatus
{
    NotReceived,
    PartiallyReceived,
    FullyReceived,
}
