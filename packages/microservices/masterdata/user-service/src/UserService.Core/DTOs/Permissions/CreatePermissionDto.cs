using System.ComponentModel.DataAnnotations;

namespace UserService.Core.DTOs.Permissions;

public class CreatePermissionDto
{
    [Required]
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}
