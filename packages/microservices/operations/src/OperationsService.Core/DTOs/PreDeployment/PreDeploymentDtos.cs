namespace OperationsService.Core.DTOs.PreDeployment;

public class PreDeploymentChecklistDto
{
    public string  Id               { get; set; } = string.Empty;
    public string  AssignmentId     { get; set; } = string.Empty;
    public string  Status           { get; set; } = "Draft";
    public string? ChecklistJson    { get; set; }
    public string? Notes            { get; set; }
    public DateTime? SubmittedAt    { get; set; }
    public string?   SubmittedByName { get; set; }
    public DateTime  CreatedAt      { get; set; }
}

public class SavePreDeploymentDto
{
    public string? ChecklistJson    { get; set; }
    public string? Notes            { get; set; }
}

public class SubmitPreDeploymentDto
{
    public string? ChecklistJson    { get; set; }
    public string? Notes            { get; set; }
}
