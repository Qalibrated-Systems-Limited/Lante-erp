namespace HrService.Core.Enums;

/// <summary>
/// H3 (P5) — where a leave application stands.
/// <para><see cref="Pending"/> covers the whole approval chain; which step it is waiting on lives in
/// <c>LeaveRequest.CurrentStep</c> and the per-step <c>LeaveApprovalLog</c> rows, so a two- or three-tier
/// chain does not need its own statuses.</para>
/// </summary>
public enum LeaveRequestStatus
{
    Pending,
    Approved,
    Rejected,
    /// <summary>Withdrawn by the employee or HR. Reserved or deducted days are returned to the balance.</summary>
    Cancelled,
}

/// <summary>
/// H3 (P5 step 5.4) — the approval tiers. The chain is derived from the leave type's flags, not from its
/// name: Line Manager always, then HR when the type requires it, then the Board when the type requires it
/// AND the request is longer than the type's board threshold (QSL policy: study leave over 2 weeks).
/// </summary>
public enum LeaveApprovalRole
{
    LineManager,
    Hr,
    Board,
}

/// <summary>H3 (P5, DS5 LEAVE_APPROVAL_LOG) — a chain step is created Pending and then actioned once.</summary>
public enum LeaveApprovalAction
{
    Pending,
    Approved,
    Rejected,
    /// <summary>Closed without a decision because an earlier step rejected, or the request was cancelled.</summary>
    Skipped,
}

/// <summary>H3 (P4/P6, DS3 LEAVE_CARRY_FORWARD) — the life of a carried-days record.</summary>
public enum CarryForwardStatus
{
    /// <summary>Carried into the new year and still usable until the 31 March expiry.</summary>
    Active,
    /// <summary>Passed 31 March with days unused — the unused remainder was taken off the entitlement.</summary>
    Expired,
    /// <summary>All carried days were used before the expiry date.</summary>
    FullyUsed,
}
