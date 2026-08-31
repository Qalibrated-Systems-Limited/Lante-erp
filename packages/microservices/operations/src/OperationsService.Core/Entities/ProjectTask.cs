using OperationsService.Core.Enums;

namespace OperationsService.Core.Entities;

public class ProjectTask : BaseEntity
{
    public string MilestoneId { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime? DueDate { get; set; }
    // PR1 — schedulable span + planned effort, so a task can appear on a Gantt and be compared
    // against the hours actually booked to it.
    public DateTime? StartDate      { get; set; }
    public decimal?  EstimatedHours { get; set; }

    /// <summary>
    /// PR1 — parent for a subtask. One level only: a subtask may not itself have children. Enforced
    /// in ProjectService.CreateTaskAsync, which also requires the parent to sit under the same
    /// milestone. Deeper trees make roll-up ambiguous and are not how work here is broken down.
    /// </summary>
    public string? ParentTaskId { get; set; }
    public string? AssignedToUserId { get; set; }

    // Status is derived from the linked Assignment — managed by service, not set directly
    public Enums.TaskStatus Status { get; set; } = Enums.TaskStatus.NotStarted;

    // Populated when manager dispatches this task as an assignment
    public string? LinkedAssignmentId { get; set; }

    public Milestone Milestone { get; set; } = null!;
    public ProjectTask? ParentTask { get; set; }
    public ICollection<ProjectTask> Subtasks { get; set; } = new List<ProjectTask>();
    public Assignment? LinkedAssignment { get; set; }
}
