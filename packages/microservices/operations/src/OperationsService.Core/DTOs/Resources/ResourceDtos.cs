namespace OperationsService.Core.DTOs.Resources;

public class AddProjectResourceDto
{
    public string ProjectId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}

public class UpdateProjectResourceDto
{
    public string? Role { get; set; }
    public bool? IsActive { get; set; }
}

public class ProjectResourceReadDto
{
    public string Id { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime AddedAt { get; set; }
}
