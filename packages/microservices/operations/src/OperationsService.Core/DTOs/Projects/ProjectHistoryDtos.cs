namespace OperationsService.Core.DTOs.Projects;

// Project audit-trail entry surfaced on the project-detail History tab.
public class ProjectHistoryReadDto
{
    public string  Id { get; set; } = string.Empty;
    public string  ProjectId { get; set; } = string.Empty;
    public string  Action { get; set; } = string.Empty;
    public string? FromValue { get; set; }
    public string? ToValue { get; set; }
    public string? Notes { get; set; }
    public string  ChangedByUserId { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
}
