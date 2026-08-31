namespace UserService.Core.DTOs.Roles;

public class UpdateRoleDto
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public bool? IsActive { get; set; }
}
