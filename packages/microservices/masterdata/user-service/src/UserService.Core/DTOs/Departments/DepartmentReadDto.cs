namespace UserService.Core.DTOs.Departments;

public class DepartmentReadDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public string? DepartmentGroupId { get; set; }
    public int UserCount { get; set; }
    public DateTime CreatedAt { get; set; }
}
