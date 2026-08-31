namespace OperationsService.Core.Entities;

public class ProjectResource : BaseEntity
{
    public string ProjectId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;

    public Project Project { get; set; } = null!;
}
