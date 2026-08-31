namespace HrService.Core.Enums;

/// <summary>H11 (P29) — a commission plan's approval path. COM-002 makes MD approval mandatory.</summary>
public enum CommissionPlanStatus
{
    Draft,
    PendingMd,
    Approved,
    Rejected,
}

/// <summary>
/// H11 (P30) — a quarterly commission statement's life.
/// <para>Computed → approved → paid through payroll. A statement under dispute is frozen: paying a figure
/// somebody is formally contesting is how a dispute becomes a grievance.</para>
/// </summary>
public enum CommissionStatementStatus
{
    /// <summary>Computed from CRM's target and attainment; not yet signed off.</summary>
    Computed,
    Approved,
    /// <summary>Handed to payroll and paid on a run.</summary>
    Paid,
    /// <summary>Formally contested — no payment until it is settled.</summary>
    Disputed,
    Cancelled,
}

/// <summary>H11 (P31, COM-006) — a dispute's life. Disputes route to HR automatically.</summary>
public enum CommissionDisputeStatus
{
    Open,
    UnderReview,
    /// <summary>Upheld — the statement is recomputed or adjusted.</summary>
    Upheld,
    /// <summary>Rejected — the original figure stands.</summary>
    Rejected,
}
