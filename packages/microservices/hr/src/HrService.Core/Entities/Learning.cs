using HrService.Core.Enums;

namespace HrService.Core.Entities;

/// <summary>
/// H7 (P22, DS1 LEARNING_DEVELOPMENT_PLAN) — one employee's plan for one year.
/// <para>Unique per employee and year, which is what makes the 31 January deadline sweep idempotent: the row's
/// existence is the answer to "has this person filed one".</para>
/// </summary>
public class LearningDevelopmentPlan : BaseEntity
{
    public string EmployeeId { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }
    public string? EmployeeName { get; set; }
    public string? DepartmentId { get; set; }

    public int PlanYear { get; set; }
    public LdpStatus Status { get; set; } = LdpStatus.Draft;

    public DateTime? SubmittedAt { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? RejectionReason { get; set; }

    /// <summary>One-way stamps, so the deadline sweep can run daily and chase each person once (HR-036).</summary>
    public DateTime? ReminderSentAt { get; set; }
    public DateTime? EscalatedAt { get; set; }

    public string? Notes { get; set; }

    public Employee? Employee { get; set; }
    public List<LdpObjective> Objectives { get; set; } = [];
}

/// <summary>
/// H7 (P22, DS2 LDP_OBJECTIVE) — one thing an employee plans to learn.
/// <para>An objective closes when a training event is logged against it (P22 design note), so the plan tracks
/// itself rather than needing a second round of data entry that nobody would do.</para>
/// </summary>
public class LdpObjective : BaseEntity
{
    public string LearningDevelopmentPlanId { get; set; } = string.Empty;

    public string Objective { get; set; } = string.Empty;
    public string? Activity { get; set; }
    public DateTime? TargetDate { get; set; }

    public LdpObjectiveStatus Status { get; set; } = LdpObjectiveStatus.Planned;
    public DateTime? CompletionDate { get; set; }
    /// <summary>The training that closed it, so "how was this met" is answerable.</summary>
    public string? CompletedByTrainingId { get; set; }
    public string? Notes { get; set; }

    public LearningDevelopmentPlan? Plan { get; set; }
}

/// <summary>
/// H7 (P23 step 23.1 / P26) — a training event: the thing that happened, once, with a cost.
/// <para>Attendance is separate (<see cref="TrainingHoursLog"/>) because one event gives different people
/// different hours, and because the cost belongs to the event while the hours belong to the person.</para>
/// </summary>
public class TrainingEvent : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string? Provider { get; set; }
    public string? Description { get; set; }

    public TrainingSource Source { get; set; } = TrainingSource.External;
    public DateTime TrainingDate { get; set; }
    public decimal DurationHours { get; set; }

    public decimal Cost { get; set; }
    public string CurrencyCode { get; set; } = "KES";
    /// <summary>Which department's L&amp;D budget the cost lands on (P26 step 26.2).</summary>
    public string? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }

    /// <summary>Set when this event satisfies a mandatory requirement — matched on the requirement's code.</summary>
    public string? MandatoryTrainingCode { get; set; }
    public string? CertificateUrl { get; set; }

    /// <summary>The knowledge-sharing session this event was raised from, when it came from one (P25 step 25.3).</summary>
    public string? KnowledgeSharingSessionId { get; set; }

    public List<TrainingHoursLog> Attendance { get; set; } = [];
}

/// <summary>
/// H7 (P23, DS2 TRAINING_HOURS_LOG) — one person's hours from one event. The YTD total is the SUM of these,
/// never a stored counter: a counter drifts the moment an event is corrected or removed.
/// </summary>
public class TrainingHoursLog : BaseEntity
{
    public string EmployeeId { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }
    public string? EmployeeName { get; set; }
    public string? DepartmentId { get; set; }

    public string TrainingEventId { get; set; } = string.Empty;
    public string? TrainingTitle { get; set; }
    public DateTime TrainingDate { get; set; }
    public decimal Hours { get; set; }

    public string? VerifiedBy { get; set; }
    public DateTime LoggedAt { get; set; } = DateTime.UtcNow;

    /// <summary>The LDP objective this closed, when it matched one.</summary>
    public string? LdpObjectiveId { get; set; }

    public Employee? Employee { get; set; }
    public TrainingEvent? TrainingEvent { get; set; }
}

/// <summary>
/// H7 (P23, HR-029) — the RULE, per HR-DEC-5: which trainings are mandatory, how long they last, and whether
/// lapsing blocks a salary increment.
/// <para><b>HR owns the rule and reads the evidence.</b> HSE completion lives in hse, anti-bribery in
/// compliance; HR does not copy either. <see cref="EvidenceSource"/> says where to look, and only
/// <see cref="TrainingEvidenceSource.Hr"/> requirements (Data Protection, which has no other home) are
/// satisfied from HR's own training log.</para>
/// </summary>
public class MandatoryTrainingRequirement : BaseEntity
{
    /// <summary>Stable key — HSE, ANTI_BRIBERY, DATA_PROTECTION.</summary>
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public TrainingEvidenceSource EvidenceSource { get; set; }
    /// <summary>How long a completion counts for. Null means it never expires.</summary>
    public int? ValidityMonths { get; set; }
    /// <summary>HR-029/HR-035 — a lapse here stops H8 proposing an increment.</summary>
    public bool BlocksIncrement { get; set; } = true;
    /// <summary>HR-034 — days past due before the lapse is treated as a red flag.</summary>
    public int GraceDays { get; set; } = 30;

    /// <summary>Null applies it to everyone; a department id narrows it.</summary>
    public string? DepartmentId { get; set; }
    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; }
}

/// <summary>
/// H7 (P25, DS2 KNOWLEDGE_SHARING_SESSION) — an internal session. HR-030 wants at least two a month
/// company-wide, which the monthly sweep checks.
/// </summary>
public class KnowledgeSharingSession : BaseEntity
{
    public string Topic { get; set; } = string.Empty;
    public string? Description { get; set; }

    public string FacilitatorEmployeeId { get; set; } = string.Empty;
    public string? FacilitatorName { get; set; }

    public DateTime SessionDate { get; set; }
    public decimal DurationHours { get; set; }

    /// <summary>The training event raised so attendees' hours count towards their target (P25 step 25.3).</summary>
    public string? TrainingEventId { get; set; }
    public int AttendeeCount { get; set; }
    public string? Notes { get; set; }

    public List<KnowledgeSharingAttendance> Attendance { get; set; } = [];
}

/// <summary>H7 (P25, DS3) — one attendee of one session.</summary>
public class KnowledgeSharingAttendance : BaseEntity
{
    public string KnowledgeSharingSessionId { get; set; } = string.Empty;
    public string EmployeeId { get; set; } = string.Empty;
    public string? EmployeeNumber { get; set; }
    public string? EmployeeName { get; set; }
    public DateTime AttendedAt { get; set; } = DateTime.UtcNow;

    public KnowledgeSharingSession? Session { get; set; }
}

/// <summary>
/// H7 (P26, DS3 LD_BUDGET) — the annual training budget for one department.
/// <para><see cref="ActualSpend"/> is maintained rather than derived because the 80% and 100% thresholds are
/// one-way alerts: they need to know the moment the line is crossed, not what the total is now. The figure is
/// recomputed from the training events whenever a cost changes, so it cannot drift.</para>
/// </summary>
public class LdBudget : BaseEntity
{
    public string DepartmentId { get; set; } = string.Empty;
    public string? DepartmentName { get; set; }
    public int Year { get; set; }

    public decimal BudgetedAmount { get; set; }
    public decimal ActualSpend { get; set; }
    public string CurrencyCode { get; set; } = "KES";

    /// <summary>One-way stamps so each threshold is announced once, not every time a course is booked.</summary>
    public DateTime? Warning80SentAt { get; set; }
    public DateTime? Exhausted100SentAt { get; set; }

    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// H7 — one raised red flag, so the daily sweep announces each one ONCE.
/// <para>The other H7 flags hang their stamp on a row that already exists (an LDP has
/// <c>ReminderSentAt</c>, a budget has <c>Warning80SentAt</c>). A mandatory-training breach and a
/// knowledge-sharing shortfall have no such row — compliance is computed, and a shortfall is the absence of
/// sessions — so this table is their stamp.</para>
/// <para>Without it the sweep re-logs the same breach every day: ticketing would dedupe the alert, but the
/// audit trail would fill with hundreds of identical rows and the daily counter would report old news as new.
/// <see cref="FlagKey"/> is what makes it unique — the employee and requirement for a breach, the month for a
/// shortfall.</para>
/// </summary>
public class LearningRedFlag : BaseEntity
{
    /// <summary>MandatoryBreach | ZeroHours | KnowledgeSharingShortfall.</summary>
    public string FlagType { get; set; } = string.Empty;
    /// <summary>Uniquely identifies the occurrence: "{employeeId}:{code}:{expiry}" or "2026-07".</summary>
    public string FlagKey { get; set; } = string.Empty;

    public string? EmployeeId { get; set; }
    public string? Detail { get; set; }
    public DateTime RaisedAt { get; set; } = DateTime.UtcNow;
}
