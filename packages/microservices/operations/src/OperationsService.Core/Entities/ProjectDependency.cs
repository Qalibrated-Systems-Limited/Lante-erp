namespace OperationsService.Core.Entities;

/// <summary>
/// PR1 — a finish-to-start link between two milestones on the same project: the successor cannot
/// start until the predecessor finishes.
///
/// Finish-to-start only, deliberately. The other three precedence types (SS/FF/SF) exist to model
/// resource-levelled schedules, which this module has explicitly chosen not to become; supporting
/// them would add a vocabulary nobody here plans in.
/// </summary>
public class MilestoneDependency : BaseEntity
{
    public string ProjectId { get; set; } = string.Empty;

    /// <summary>Must finish first.</summary>
    public string PredecessorMilestoneId { get; set; } = string.Empty;
    /// <summary>Cannot start until the predecessor finishes.</summary>
    public string SuccessorMilestoneId   { get; set; } = string.Empty;

    /// <summary>Working days to wait after the predecessor finishes. Negative values are rejected —
    /// a lead would let a successor start before its predecessor ends, which contradicts FS.</summary>
    public int LagDays { get; set; }

    public Project Project { get; set; } = null!;
}

/// <summary>PR1 — finish-to-start link between two tasks. Same rules as MilestoneDependency.</summary>
public class TaskDependency : BaseEntity
{
    public string ProjectId { get; set; } = string.Empty;
    public string PredecessorTaskId { get; set; } = string.Empty;
    public string SuccessorTaskId   { get; set; } = string.Empty;
    public int LagDays { get; set; }

    public Project Project { get; set; } = null!;
}
