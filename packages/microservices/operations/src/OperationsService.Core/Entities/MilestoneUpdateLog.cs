namespace OperationsService.Core.Entities;

/// <summary>
/// O3 — MILESTONE_UPDATE_LOG: append-only trail of milestone progress updates. Distinct from
/// ProjectHistory (which is project-level field changes) — this is the DFD daily-by-5PM update
/// requirement. A PM logs a progress note each day; the background sweep writes a breach row
/// (IsBreachAlert) for any in-progress milestone with no update by the deadline.
/// </summary>
public class MilestoneUpdateLog : BaseEntity
{
    public string  MilestoneId     { get; set; } = string.Empty;
    public string  ProjectId       { get; set; } = string.Empty;
    public string? Note            { get; set; }
    public string  StatusAtUpdate  { get; set; } = string.Empty;  // MilestoneStatus.ToString()
    public int?    ProgressPct      { get; set; }
    public bool    IsBreachAlert   { get; set; }                  // system-written 5PM breach
    public string  UpdatedByUserId { get; set; } = string.Empty;

    public Milestone Milestone { get; set; } = null!;
}
