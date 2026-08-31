using HrService.Core.Enums;

namespace HrService.Core.Entities;

/// <summary>
/// H9 (P14, DS2 KPI_SCORECARD) — the scorecard for one role for one year.
/// <para>Held per <see cref="Position"/> rather than per employee: HR-014 wants one scorecard per role, and
/// every holder of that role is measured on the same things. What differs between people is their TARGETS,
/// which is why those live on <see cref="KpiTarget"/>.</para>
/// </summary>
public class KpiScorecard : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>The role this measures. Null makes it a company-wide default for staff with no position set.</summary>
    public string? PositionId { get; set; }
    public string? PositionTitle { get; set; }

    public int Year { get; set; }

    /// <summary>
    /// HR-019 — a final appraisal score below this automatically raises a PIP. Configurable per scorecard
    /// (P15 design note), because what counts as underperformance differs by role.
    /// </summary>
    public decimal PipThreshold { get; set; } = 50m;

    public bool IsActive { get; set; } = true;

    public List<KpiScorecardItem> Items { get; set; } = [];
}

/// <summary>
/// H9 (P14 step 14.2, DS3 KPI_SCORECARD_ITEM) — one line of a scorecard.
/// <para><b>Weights must total exactly 100</b> across a scorecard's active items (P14 design note). A
/// scorecard that sums to 93 does not produce a wrong score in some obvious way — it produces a plausible one
/// that is quietly 7% too low for everybody on it.</para>
/// </summary>
public class KpiScorecardItem : BaseEntity
{
    public string KpiScorecardId { get; set; } = string.Empty;

    public string ItemName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal WeightPercent { get; set; }

    public KpiMeasurementType MeasurementType { get; set; } = KpiMeasurementType.Qualitative;
    /// <summary>Where the actual comes from — see <see cref="KpiTargetSource"/>.</summary>
    public KpiTargetSource TargetSource { get; set; } = KpiTargetSource.Manual;
    /// <summary>"KES", "tickets", "%" — shown beside the numbers so a target reads as something real.</summary>
    public string? Unit { get; set; }

    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public KpiScorecard? Scorecard { get; set; }
}

/// <summary>
/// H9 (P14 step 14.4, DS5 KPI_TARGET) — what ONE employee is aiming at on ONE item for a year.
/// <para>The actual is kept here as well as on the appraisal because a target is tracked all year, while an
/// appraisal is a snapshot taken twice. The appraisal copies the actual at the moment it is scored, so
/// re-reading an old appraisal shows what it was scored on, not what the number has since become.</para>
/// </summary>
public class KpiTarget : BaseEntity
{
    public string EmployeeId { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }
    public string? EmployeeName { get; set; }

    public string KpiScorecardId { get; set; } = string.Empty;
    public string KpiScorecardItemId { get; set; } = string.Empty;
    public string? ItemName { get; set; }
    public int Year { get; set; }

    public decimal TargetValue { get; set; }
    public decimal ActualValue { get; set; }
    /// <summary>How the actual last got here — a system source, or a person.</summary>
    public KpiTargetSource ActualSource { get; set; } = KpiTargetSource.Manual;
    public DateTime? ActualUpdatedAt { get; set; }

    public string? Notes { get; set; }

    public Employee? Employee { get; set; }
}

/// <summary>H9 (P15, DS1 APPRAISAL_CYCLE) — an appraisal window. Opening one raises an appraisal per active
/// employee; the cycle's existence per (year, type) is what stops that happening twice.</summary>
public class AppraisalCycle : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public AppraisalCycleType CycleType { get; set; }
    public int Year { get; set; }

    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    public AppraisalCycleStatus Status { get; set; } = AppraisalCycleStatus.Draft;
    public DateTime? OpenedAt { get; set; }
    public string? OpenedBy { get; set; }
    public DateTime? ClosedAt { get; set; }
    public string? Notes { get; set; }

    public int AppraisalCount { get; set; }
}

/// <summary>
/// H9 (P15, DS2 APPRAISAL) — one employee's appraisal for one cycle.
/// <para><b>The four steps are enforced in order</b> (HR-018): self-assessment, line manager, MD, HR. Each
/// stamps its own timestamp and no step can be taken before the one before it — an appraisal that reached the
/// MD without the employee ever seeing it is not an appraisal, it is a verdict.</para>
/// <para>MD sign-off is what triggers the PIP check (P17 step 17.1) — not HR recording, because by then the
/// decision has already been filed.</para>
/// </summary>
public class Appraisal : BaseEntity
{
    public string AppraisalCycleId { get; set; } = string.Empty;
    public string? CycleName { get; set; }
    public int Year { get; set; }

    public string EmployeeId { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }
    public string? EmployeeName { get; set; }
    public string? DepartmentId { get; set; }
    public string? PositionTitle { get; set; }

    public string? KpiScorecardId { get; set; }
    public string? ScorecardName { get; set; }
    /// <summary>Copied at scoring time so a later change to the scorecard cannot move an old verdict.</summary>
    public decimal PipThreshold { get; set; }

    public AppraisalStatus Status { get; set; } = AppraisalStatus.PendingSelf;

    // ── The four steps (HR-018) ──
    public DateTime? SelfAssessmentSubmittedAt { get; set; }
    public string? SelfAssessmentBy { get; set; }
    public DateTime? LineManagerReviewedAt { get; set; }
    public string? LineManagerBy { get; set; }
    public DateTime? MdSignedOffAt { get; set; }
    public string? MdSignedOffBy { get; set; }
    public DateTime? HrRecordedAt { get; set; }
    public string? HrRecordedBy { get; set; }

    public decimal? SelfScore { get; set; }
    public decimal? LineManagerScore { get; set; }
    /// <summary>The aggregate from the 360 round, when one ran (P16 step 16.6).</summary>
    public decimal? Feedback360Score { get; set; }
    /// <summary>The line manager's score is the verdict; the MD signs it off rather than re-scoring.</summary>
    public decimal? FinalScore { get; set; }

    public string? SelfComments { get; set; }
    public string? LineManagerComments { get; set; }
    public string? MdComments { get; set; }
    public string? HrComments { get; set; }
    /// <summary>HR-020 — development needs identified here feed the employee's LDP.</summary>
    public string? TrainingNeeds { get; set; }

    public bool PipTriggered { get; set; }
    public string? PipId { get; set; }

    public Appraisal? Parent { get; set; }
    public List<AppraisalItemScore> ItemScores { get; set; } = [];
}

/// <summary>
/// H9 (P15 step 15.2, DS4 KPI_ACTUAL) — one item's score on one appraisal.
/// <para>Target and actual are COPIED here rather than read through to <see cref="KpiTarget"/>, so an
/// appraisal signed in July still shows the numbers it was signed on after the year-to-date figures move.</para>
/// </summary>
public class AppraisalItemScore : BaseEntity
{
    public string AppraisalId { get; set; } = string.Empty;
    public string? KpiTargetId { get; set; }
    public string? KpiScorecardItemId { get; set; }

    public string ItemName { get; set; } = string.Empty;
    public decimal WeightPercent { get; set; }
    public KpiMeasurementType MeasurementType { get; set; }
    public KpiTargetSource Source { get; set; }
    public string? Unit { get; set; }

    public decimal TargetValue { get; set; }
    public decimal ActualValue { get; set; }

    /// <summary>0–100 on the item itself, before its weight is applied.</summary>
    public decimal? SelfScore { get; set; }
    public decimal? ManagerScore { get; set; }
    /// <summary>The score that counts — the manager's, falling back to the employee's where none was given.</summary>
    public decimal? FinalScore { get; set; }

    public string? Comments { get; set; }
    public int DisplayOrder { get; set; }

    public Appraisal? Appraisal { get; set; }
}

/// <summary>
/// H9 (P16, DS3 FEEDBACK_360) — one reviewer's contribution to a 360 round.
/// <para><b>Individual scores are never shown to the employee</b> (P16 summary) — only the aggregate. The
/// reviewer is recorded because HR needs to know who has responded and who to chase, but no read path returns
/// the reviewer alongside their score.</para>
/// </summary>
public class Feedback360 : BaseEntity
{
    public string AppraisalId { get; set; } = string.Empty;
    public string EmployeeId { get; set; } = string.Empty;

    public string ReviewerEmployeeId { get; set; } = string.Empty;
    /// <summary>Kept for chasing non-responders. Deliberately absent from the aggregate read model.</summary>
    public string? ReviewerName { get; set; }
    public Feedback360ReviewerType ReviewerType { get; set; }

    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SubmittedAt { get; set; }
    public DateTime? ReminderSentAt { get; set; }

    /// <summary>0–100 overall. Per-competency detail is held as JSON so a tenant can shape its own form
    /// without a schema change.</summary>
    public decimal? OverallScore { get; set; }
    public string? ScoresJson { get; set; }
    public string? Comments { get; set; }
}

/// <summary>
/// H9 (P17, DS3 PERFORMANCE_IMPROVEMENT_PLAN) — raised automatically when an appraisal is signed off below
/// the scorecard's threshold (HR-019).
/// <para>The trigger score is stored because the threshold can change: a PIP has to remain explicable against
/// the rule that was in force when it was raised.</para>
/// </summary>
public class PerformanceImprovementPlan : BaseEntity
{
    public string EmployeeId { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }
    public string? EmployeeName { get; set; }

    public string? AppraisalId { get; set; }
    public decimal TriggerScore { get; set; }
    public decimal Threshold { get; set; }

    public string? Objectives { get; set; }
    public string? SupportProvided { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime ReviewDate { get; set; }
    public DateTime? SecondReviewDate { get; set; }

    public PipStatus Status { get; set; } = PipStatus.Active;
    public string? Outcome { get; set; }
    public DateTime? ClosedAt { get; set; }
    public string? ClosedBy { get; set; }

    /// <summary>HR-020 — the LDP objective raised from this plan's training gaps, when one was.</summary>
    public string? LinkedLdpObjectiveId { get; set; }

    public Employee? Employee { get; set; }
}
