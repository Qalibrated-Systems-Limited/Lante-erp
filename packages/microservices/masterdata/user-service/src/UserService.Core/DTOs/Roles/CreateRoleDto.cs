using System.ComponentModel.DataAnnotations;

namespace UserService.Core.DTOs.Roles;

public class CreateRoleDto
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}
