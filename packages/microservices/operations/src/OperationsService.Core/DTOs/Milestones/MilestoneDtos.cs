using OperationsService.Core.DTOs.Tasks;
using OperationsService.Core.Enums;

namespace OperationsService.Core.DTOs.Milestones;

public class CreateMilestoneDto
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Order { get; set; }
    public DateTime DueDate { get; set; }
    /// <summary>PR1 — needed for a Gantt bar; a due date alone gives only a point.</summary>
    public DateTime? StartDate { get; set; }
    public decimal? PlannedAmount { get; set; }
    public bool IsBillable { get; set; }
    public bool LdApplies  { get; set; }
}

public class UpdateMilestoneDto
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public int? Order { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? StartDate { get; set; }
    public MilestoneStatus? Status { get; set; }
    public decimal? PlannedAmount { get; set; }
    public bool? IsBillable { get; set; }
    public bool? LdApplies  { get; set; }
    public int?  ProgressPct { get; set; }
}

public class SignOffMilestoneDto
{
    public string SignOffBy { get; set; } = string.Empty; // who accepted (client rep / authorising name)
    public string? Notes    { get; set; }
}

// O3 — daily milestone progress update (the "by 5PM" requirement).
public class AddMilestoneUpdateDto
{
    public string? Note { get; set; }
    public int?    ProgressPct { get; set; }
    public MilestoneStatus? Status { get; set; }
}

public class MilestoneUpdateLogReadDto
{
    public string  Id { get; set; } = string.Empty;
    public string  MilestoneId { get; set; } = string.Empty;
    public string  ProjectId { get; set; } = string.Empty;
    public string? Note { get; set; }
    public string  StatusAtUpdate { get; set; } = string.Empty;
    public int?    ProgressPct { get; set; }
    public bool    IsBreachAlert { get; set; }
    public string  UpdatedByUserId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class MilestoneReadDto
{
    // PR1 — schedule + baseline, so a list can show slippage without a second call.
    public DateTime? StartDate     { get; set; }
    public DateTime? BaselineStart { get; set; }
    public DateTime? BaselineDue   { get; set; }
    public int?      ScheduleVarianceDays { get; set; }

    public string Id { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Order { get; set; }
    public DateTime DueDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal? PlannedAmount { get; set; }
    public bool IsBillable { get; set; }
    public bool LdApplies  { get; set; }
    public int      ProgressPct { get; set; }
    public decimal  LdAmount { get; set; }
    public string?   SignOffBy { get; set; }
    public DateTime? SignOffAt { get; set; }
    public DateTime? LastUpdatedAt { get; set; }
    public int TaskCount { get; set; }
    public int CompletedTaskCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<TaskReadDto> Tasks { get; set; } = new();
}
