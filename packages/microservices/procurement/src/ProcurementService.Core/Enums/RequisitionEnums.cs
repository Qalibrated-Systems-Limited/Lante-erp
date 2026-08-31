namespace ProcurementService.Core.Enums;

/// <summary>P2 (LPO steps 1–2) — purchase requisition lifecycle. A PR is built as Draft, submitted (which
/// runs the hard budget check), then reviewed by the Department Head within the 2-business-day SLA.</summary>
public enum PrStatus
{
    Draft,            // being built; not yet budget-checked
    PendingDeptHead,  // submitted, budget cleared, awaiting Dept-Head review
    Approved,         // Dept-Head approved → routed to Procurement (triggers P3 quotation)
    Rejected,         // Dept-Head rejected (reason mandatory)
    Cancelled,
}

/// <summary>PR approval-log action kinds.</summary>
public enum PrAction
{
    Submitted,
    Approved,
    Rejected,
    Escalated,   // SLA breach → MD
}
