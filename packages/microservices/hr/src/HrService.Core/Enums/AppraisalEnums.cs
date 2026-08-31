namespace HrService.Core.Enums;

/// <summary>H9 (P14) — how a KPI item is measured.</summary>
public enum KpiMeasurementType
{
    /// <summary>Has a number: actual against target (revenue, tickets closed, uptime).</summary>
    Quantitative,
    /// <summary>Judged, not counted — the assessor rates it directly out of 100.</summary>
    Qualitative,
}

/// <summary>
/// H9 (P14 step 14.5) — where an item's ACTUAL value comes from.
/// <para>Anything other than <see cref="Manual"/> is filled by the system, which is the point: a KPI nobody
/// has to remember to update is a KPI that stays true.</para>
/// </summary>
public enum KpiTargetSource
{
    /// <summary>Entered by the employee or the line manager at appraisal time.</summary>
    Manual,
    /// <summary>H4's annual attendance scorecard (ATT-008) — already built, already scored out of 100.</summary>
    Attendance,
    /// <summary>The 360 aggregate for this appraisal (P16 step 16.6).</summary>
    Feedback360,
    /// <summary>
    /// Per-employee collected revenue (HR-016). DECLARED BUT NOT WIRED: finance holds revenue by customer and
    /// cost centre, not by salesperson — the per-employee figure lives in <c>crm.RevenueSnapshot</c>, and which
    /// module owns the sales target is the open question H11 has to settle. Until then an item on this source
    /// is scored manually and says so, rather than silently reading zero.
    /// </summary>
    Revenue,
}

/// <summary>H9 (P15) — the two appraisal windows a year.</summary>
public enum AppraisalCycleType
{
    MidYear,
    EndOfYear,
}

public enum AppraisalCycleStatus
{
    /// <summary>Created but not yet issued to staff.</summary>
    Draft,
    /// <summary>Appraisals raised; the workflow is running.</summary>
    Open,
    Closed,
}

/// <summary>
/// H9 (P15, HR-018) — the four-step appraisal workflow, in order. Each step is a status AND a timestamp, and
/// a step cannot be taken until the one before it has been (P15 design note).
/// </summary>
public enum AppraisalStatus
{
    PendingSelf,
    PendingLineManager,
    PendingMd,
    PendingHr,
    Completed,
    /// <summary>Abandoned — the employee left, or the cycle was cancelled.</summary>
    Cancelled,
}

/// <summary>H9 (P16) — who is giving the feedback. Peers and subordinates are averaged separately so one
/// group cannot be drowned out by the other.</summary>
public enum Feedback360ReviewerType
{
    Peer,
    Subordinate,
    LineManager,
}

/// <summary>H9 (P17) — a performance improvement plan's life.</summary>
public enum PipStatus
{
    Active,
    /// <summary>Improved — closed successfully.</summary>
    Completed,
    /// <summary>Review dates pushed out; still running.</summary>
    Extended,
    /// <summary>No improvement — handed to the disciplinary process (H10).</summary>
    EscalatedToDisciplinary,
}
