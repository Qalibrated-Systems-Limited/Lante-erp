using System.ComponentModel.DataAnnotations;

namespace UserService.Core.DTOs.Departments;

public class CreateDepartmentDto
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public string? DepartmentGroupId { get; set; }
}
