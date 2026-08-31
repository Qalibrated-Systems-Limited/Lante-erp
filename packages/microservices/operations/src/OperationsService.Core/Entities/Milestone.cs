using OperationsService.Core.Enums;

namespace OperationsService.Core.Entities;

public class Milestone : BaseEntity
{
    public string ProjectId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Order { get; set; }
    public DateTime DueDate { get; set; }

    // PR1 — a milestone needs a start to be schedulable at all; DueDate alone gives a point, not a bar.
    public DateTime? StartDate { get; set; }

    // PR1 — the dates as approved. Frozen when the project is activated and only moved by an
    // approved change request, so slippage stays measurable after the live dates are revised.
    public DateTime? BaselineStart { get; set; }
    public DateTime? BaselineDue   { get; set; }
    public MilestoneStatus Status { get; set; } = MilestoneStatus.NotStarted;
    public decimal? PlannedAmount { get; set; }

    // O1 — billing + liquidated-damages flags drive the O3 invoicing / LD-calc seams.
    public bool IsBillable { get; set; }
    public bool LdApplies  { get; set; }

    // O1 — client sign-off (the O3 milestone→Finance invoice trigger).
    public string?   SignOffBy { get; set; }
    public DateTime? SignOffAt { get; set; }

    // O1 — last time the milestone itself was progressed (distinct from the record's UpdatedAt).
    // Drives the O3 daily 5PM staleness/breach check.
    public DateTime? LastUpdatedAt { get; set; }

    // O3 — current progress %, accrued liquidated damages, and the day a 5PM breach was last alerted
    // (so the sweep alerts at most once per day per milestone).
    public int      ProgressPct     { get; set; }
    public decimal  LdAmount        { get; set; }
    public DateTime? BreachAlertedOn { get; set; }

    public Project Project { get; set; } = null!;
    public ICollection<MilestoneUpdateLog> UpdateLogs { get; set; } = new List<MilestoneUpdateLog>();
    public ICollection<ProjectTask> Tasks { get; set; } = new List<ProjectTask>();
    public ICollection<CostEntry> CostEntries { get; set; } = new List<CostEntry>();

    /// <summary>Days late against the approved baseline; negative is ahead. Null until a baseline
    /// is set. Uses the actual finish once signed off, otherwise the current planned due date.</summary>
    public int? ScheduleVarianceDays =>
        BaselineDue is null ? null : (int)((SignOffAt?.Date ?? DueDate.Date) - BaselineDue.Value.Date).TotalDays;
}
