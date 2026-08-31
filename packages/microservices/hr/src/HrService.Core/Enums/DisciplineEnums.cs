namespace HrService.Core.Enums;

/// <summary>
/// H10 (P18, HR-021) — a disciplinary case's stages, in order: incident → show cause → hearing → outcome →
/// appeal. Each stage refuses to run before the one before it, because the sequence IS the fairness.
/// </summary>
public enum DisciplinaryStatus
{
    /// <summary>Incident logged; nothing has been put to the employee yet.</summary>
    Open,
    /// <summary>Show-cause letter issued; the response window is running.</summary>
    ShowCauseIssued,
    /// <summary>Employee has responded (or the window closed) and a hearing is set.</summary>
    AwaitingHearing,
    /// <summary>Outcome recorded; the right of appeal is open.</summary>
    OutcomeRecorded,
    /// <summary>Employee has appealed; awaiting the appeal decision.</summary>
    Appealed,
    Closed,
    /// <summary>Dropped — no case to answer.</summary>
    Withdrawn,
}

/// <summary>H10 (P18 step 18.4) — what a hearing decided.</summary>
public enum DisciplinaryOutcome
{
    None,
    /// <summary>No case to answer.</summary>
    NoAction,
    Warning,
    Suspension,
    /// <summary>Routes to separation (P18 step 18.4b → P21).</summary>
    Termination,
}

/// <summary>
/// H10 (P19, HR-022) — warning tiers and how long they stand. The QSL policy is six months for a verbal
/// warning and twelve for anything written; the durations live on the enum's consumer, not here, so a tenant
/// can change them without a new tier.
/// </summary>
public enum WarningType
{
    Verbal,
    Written,
    FinalWritten,
}

/// <summary>H10 (P20, HR-023) — a grievance's life.</summary>
public enum GrievanceStatus
{
    Submitted,
    Acknowledged,
    Investigating,
    Resolved,
    PartiallyResolved,
    /// <summary>Unresolved — escalated to the MD for a formal hearing.</summary>
    Escalated,
    Withdrawn,
}

/// <summary>H10 (P21, HR-024) — how someone leaves. Each carries a different notice and dues treatment.</summary>
public enum SeparationType
{
    Resignation,
    Termination,
    Retirement,
    /// <summary>A fixed-term contract simply ending — the H2 renewal flow's "let expire".</summary>
    EndOfContract,
    Death,
}

/// <summary>
/// H10 (P21) — a separation's approval path. Only <see cref="Paid"/> deactivates the employee: someone is on
/// the payroll until they have actually been paid what they are owed.
/// </summary>
public enum SeparationStatus
{
    Draft,
    PendingMd,
    Approved,
    Paid,
    Cancelled,
}
