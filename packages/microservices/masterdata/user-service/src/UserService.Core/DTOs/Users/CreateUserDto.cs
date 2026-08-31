using System.ComponentModel.DataAnnotations;

namespace UserService.Core.DTOs.Users;

public class CreateUserDto
{
    [Required]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    public string LastName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    public string MobileNumber { get; set; } = string.Empty;

    public string? DepartmentId { get; set; }

    public string? BranchId { get; set; }

    public string? TenantId { get; set; }

    public List<string>? RoleIds { get; set; }
}
