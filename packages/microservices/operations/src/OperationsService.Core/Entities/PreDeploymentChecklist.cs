namespace OperationsService.Core.Entities;

public class PreDeploymentChecklist : BaseEntity
{
    public string AssignmentId      { get; set; } = string.Empty;
    public string Status            { get; set; } = "Draft";   // Draft | Submitted
    public string? ChecklistJson    { get; set; }              // Full JSON blob from frontend
    public string? Notes            { get; set; }
    public DateTime? SubmittedAt    { get; set; }
    public string?   SubmittedById  { get; set; }
    public string?   SubmittedByName { get; set; }

    public Assignment Assignment { get; set; } = null!;
}
