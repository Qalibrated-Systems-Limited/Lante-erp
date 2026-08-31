namespace UserService.Core.DTOs.Departments;

public class UpdateDepartmentDto
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public bool? IsActive { get; set; }
    public string? DepartmentGroupId { get; set; }
}
