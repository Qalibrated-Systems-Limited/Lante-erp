namespace OperationsService.Core.DTOs.Tasks;

public class CreateTaskDto
{
    // PR1 — schedulable span, planned effort, and subtask parenting (one level).
    public DateTime? StartDate      { get; set; }
    public decimal?  EstimatedHours { get; set; }
    public string?   ParentTaskId   { get; set; }

    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime? DueDate { get; set; }
    public string? AssignedToUserId { get; set; }
}

public class UpdateTaskDto
{
    public DateTime? StartDate      { get; set; }
    public decimal?  EstimatedHours { get; set; }

    public string? Title { get; set; }
    public string? Description { get; set; }
    public DateTime? DueDate { get; set; }
    public string? AssignedToUserId { get; set; }
    public Enums.TaskStatus? Status { get; set; }
}

public class DispatchTaskDto
{
    public string? AssignedToUserId { get; set; }
    public string? AssignedToUserName { get; set; }
    public string? Notes { get; set; }
}

public class TaskReadDto
{
    public DateTime? StartDate      { get; set; }
    public decimal?  EstimatedHours { get; set; }
    public string?   ParentTaskId   { get; set; }

    public string Id { get; set; } = string.Empty;
    public string MilestoneId { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime? DueDate { get; set; }
    public string? AssignedToUserId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? LinkedAssignmentId { get; set; }
    public DateTime CreatedAt { get; set; }
}
