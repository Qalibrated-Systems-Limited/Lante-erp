namespace OperationsService.Core.DTOs.Schedule;

/// <summary>PR1 — everything a Gantt needs for one project, in one call.</summary>
public class ProjectScheduleDto
{
    public string ProjectId   { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;

    /// <summary>Null until the project is baselined; the UI uses this to explain why variance is blank.</summary>
    public DateTime? BaselineSetAt { get; set; }

    public DateTime PlannedStart  { get; set; }
    public DateTime PlannedFinish { get; set; }

    /// <summary>Days late on the worst milestone; negative means everything is ahead.</summary>
    public int WorstVarianceDays { get; set; }

    public List<ScheduleItemDto> Items { get; set; } = new();
    public List<DependencyDto> Dependencies { get; set; } = new();
}

public class ScheduleItemDto
{
    public string Id     { get; set; } = string.Empty;
    public string Title  { get; set; } = string.Empty;
    public int    Order  { get; set; }
    public string Status { get; set; } = string.Empty;

    public DateTime? StartDate { get; set; }
    public DateTime  DueDate   { get; set; }
    public DateTime? BaselineStart { get; set; }
    public DateTime? BaselineDue   { get; set; }

    public int  ProgressPct { get; set; }
    public DateTime? SignOffAt { get; set; }

    /// <summary>Days late against baseline; negative is ahead. Null when no baseline exists.</summary>
    public int? ScheduleVarianceDays { get; set; }

    /// <summary>On the longest dependency chain — slipping this slips the project.</summary>
    public bool IsCritical { get; set; }

    public List<string> PredecessorIds { get; set; } = new();
}

public class DependencyDto
{
    public string Id { get; set; } = string.Empty;
    public string PredecessorId { get; set; } = string.Empty;
    public string SuccessorId   { get; set; } = string.Empty;
    public int    LagDays       { get; set; }
}

public class CreateDependencyDto
{
    public string PredecessorId { get; set; } = string.Empty;
    public string SuccessorId   { get; set; } = string.Empty;
    public int    LagDays       { get; set; }
}
