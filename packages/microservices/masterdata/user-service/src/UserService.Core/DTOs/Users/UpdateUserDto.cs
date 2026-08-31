using System.ComponentModel.DataAnnotations;

namespace UserService.Core.DTOs.Users;

public class UpdateUserDto
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? MobileNumber { get; set; }
    public string? DepartmentId { get; set; }

    [EmailAddress]
    public string? Email { get; set; }
}
