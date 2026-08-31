namespace OperationsService.Core.DTOs.Board;

// PR4c — board, calendar and workload. Read-only projections over milestones and tasks; the views
// re-present work PR1 made complete rather than storing anything new.

public class BoardTaskDto
{
    public string  Id          { get; set; } = string.Empty;
    public string  MilestoneId { get; set; } = string.Empty;
    public string? MilestoneTitle { get; set; }
    public string  Title       { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string  Status      { get; set; } = string.Empty;
    public string? AssignedToUserId { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? DueDate   { get; set; }
    public decimal?  EstimatedHours { get; set; }
    public string?   ParentTaskId   { get; set; }
    /// <summary>Past its due date and not yet done.</summary>
    public bool      IsOverdue { get; set; }
}

public class BoardMilestoneDto
{
    public string  Id     { get; set; } = string.Empty;
    public string  Title  { get; set; } = string.Empty;
    public string  Status { get; set; } = string.Empty;
    public int     ProgressPct { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime  DueDate   { get; set; }
    public bool      IsOverdue { get; set; }
}

public class ProjectBoardDto
{
    public string ProjectId { get; set; } = string.Empty;
    public List<BoardMilestoneDto> Milestones { get; set; } = new();
    public List<BoardTaskDto>      Tasks      { get; set; } = new();
}

public class WorkloadRowDto
{
    public string  UserId    { get; set; } = string.Empty;
    public int     TaskCount { get; set; }
    /// <summary>Estimated hours on tasks that overlap the window. Null estimates count as zero hours.</summary>
    public decimal EstimatedHours { get; set; }
    /// <summary>Tasks with no estimate — the hours figure understates the load by this many tasks.</summary>
    public int     UnestimatedCount { get; set; }
    public int     OverdueCount     { get; set; }
    public int     ProjectCount     { get; set; }
    public List<string> ProjectNames { get; set; } = new();
}

public class WorkloadDto
{
    public DateTime From { get; set; }
    public DateTime To   { get; set; }
    /// <summary>Working days in the window — the yardstick a person's hours are read against.</summary>
    public int WorkingDays { get; set; }
    public List<WorkloadRowDto> Rows { get; set; } = new();
    /// <summary>Tasks in the window with nobody assigned. Unowned work is the thing a workload view should surface.</summary>
    public int UnassignedCount { get; set; }
    public decimal UnassignedHours { get; set; }
}
