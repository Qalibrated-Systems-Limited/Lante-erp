namespace OperationsService.Core.Entities;

public class ProjectHistory : BaseEntity
{
    public string ProjectId { get; set; } = string.Empty;
    public string ChangedByUserId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string? Notes { get; set; }
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;

    public Project Project { get; set; } = null!;
}
