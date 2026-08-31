namespace HrService.Core.Enums;

/// <summary>
/// H12 — why a requisition was raised. The distinction is not cosmetic: a replacement fills a post the
/// establishment already carries, whereas a new role or an expansion ASKS FOR ONE MORE. Only the latter two
/// can push headcount past the position's approved establishment, and the approver is told which they are
/// looking at.
/// </summary>
public enum RequisitionType
{
    /// <summary>Backfilling a post someone has left — sits inside the existing establishment.</summary>
    Replacement,
    /// <summary>A post the position has approved but has never filled.</summary>
    NewRole,
    /// <summary>Growing the establishment itself — the approval IS the establishment increase.</summary>
    Expansion,
}

/// <summary>
/// H12 — a requisition's approval path, mirroring H8's increment flow. Nothing may be advertised from a
/// requisition that has not been approved, because a vacancy is a commitment to pay someone.
/// </summary>
public enum RequisitionStatus
{
    Draft,
    PendingApproval,
    Approved,
    Rejected,
    /// <summary>Withdrawn before a decision, or after approval but before anyone was hired.</summary>
    Cancelled,
}

/// <summary>H12 — where a vacancy was advertised. Internal-only postings are the promotion path.</summary>
public enum PostingChannel
{
    Internal,
    External,
    Both,
}

/// <summary>
/// H12 — a vacancy's life. <see cref="Filled"/> is reached by hiring the last approved head, not by someone
/// deciding it looks finished; <see cref="Closed"/> is a human closing it early or the closing date passing.
/// </summary>
public enum VacancyStatus
{
    Open,
    /// <summary>Past its closing date or closed by hand — no new applications, but those in flight continue.</summary>
    Closed,
    /// <summary>Every approved head hired.</summary>
    Filled,
    Cancelled,
}

/// <summary>H12 — how an applicant reached us. Feeds the source-effectiveness view: which channels actually hire.</summary>
public enum ApplicantSource
{
    Website,
    JobBoard,
    /// <summary>Referred by a member of staff — <c>ReferredByEmployeeId</c> says who.</summary>
    Referral,
    /// <summary>An existing employee applying — <c>InternalEmployeeId</c> links them to their record.</summary>
    Internal,
    Agency,
    WalkIn,
}

/// <summary>
/// H12 — an applicant's progress. <b>The order is enforced.</b> Nobody is interviewed before being screened
/// and nobody is offered before a panel has recommended them: skipping either turns the pipeline into a
/// record of what happened to be typed, which is no use when a rejected candidate asks why.
/// </summary>
public enum ApplicantStatus
{
    Applied,
    /// <summary>Passed screening — in the running, interviews may be scheduled.</summary>
    Shortlisted,
    /// <summary>At least one interview has been held.</summary>
    Interviewing,
    /// <summary>A panel has recommended proceeding — an offer may now be prepared.</summary>
    Recommended,
    OfferMade,
    Hired,
    /// <summary>Rejected by us, at any stage. <c>RejectionReason</c> carries the stage and the why.</summary>
    Rejected,
    /// <summary>The candidate pulled out.</summary>
    Withdrawn,
}

/// <summary>H12 — which interview this is. A vacancy need not use every stage.</summary>
public enum InterviewStage
{
    /// <summary>A first conversation, often by phone, to confirm the basics stack up.</summary>
    Screening,
    Technical,
    Panel,
    /// <summary>The last one, usually with the hiring manager or MD.</summary>
    Final,
}

/// <summary>H12 — what an interview panel concluded. <see cref="Proceed"/> is the gate an offer stands behind.</summary>
public enum InterviewRecommendation
{
    /// <summary>Not yet scored — the interview is scheduled but has not been held.</summary>
    Pending,
    Proceed,
    /// <summary>Keep on file — acceptable, but not ahead of others.</summary>
    Hold,
    Reject,
}

/// <summary>
/// H12 — an offer's path. The salary is approved BEFORE the offer goes out (as H8 does for increments), and
/// whoever prepared it cannot approve it. <see cref="Lapsed"/> is reached by the response deadline passing,
/// which the daily sweep stamps rather than leaving offers open for ever.
/// </summary>
public enum OfferStatus
{
    Draft,
    PendingApproval,
    /// <summary>Salary approved; not yet sent to the candidate.</summary>
    Approved,
    /// <summary>Sent — the response clock is running.</summary>
    Issued,
    Accepted,
    Declined,
    /// <summary>The response deadline passed with no answer.</summary>
    Lapsed,
    /// <summary>Pulled by us before it was answered.</summary>
    Withdrawn,
}
