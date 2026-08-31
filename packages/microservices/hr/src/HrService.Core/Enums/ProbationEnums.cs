namespace HrService.Core.Enums;

/// <summary>H2 (HR-003) — probation milestones. QSL policy is a 6-month probation: Day 90 is a review,
/// Day 180 is the confirmation decision.</summary>
public enum ProbationReviewType
{
    ThreeMonth,
    SixMonth,
    /// <summary>A follow-up review created when probation is extended (design note: extension requires a new
    /// review record with new dates).</summary>
    Extended,
}

/// <summary>H2 (P3 step 3.3) — the three outcomes, plus the Pending state a scheduled review starts in.</summary>
public enum ProbationOutcome
{
    Pending,
    Confirmed,      // → Employee.Status = Active, ConfirmationDate stamped
    Extended,       // → a new review is scheduled
    Terminated,     // → hands off to H10 separation (Process 21)
}

/// <summary>H2 (P33) — how a fixed-term contract was resolved.</summary>
public enum ContractRenewalOutcome
{
    Pending,
    Renewed,                // new end date; the new contract goes into the H1 document vault
    ConvertedToPermanent,   // employment_type = Permanent; future alerts closed
    Expired,                // no action taken — hands off to H10 separation
}
