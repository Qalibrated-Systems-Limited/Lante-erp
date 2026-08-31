namespace UserService.Core.DTOs.Auth;

public class LoginResponseDto
{
    public string Token { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? DepartmentId { get; set; }
    public List<string> DepartmentIds { get; set; } = new();
    public List<string> UserRoles { get; set; } = new();
    public List<string> Permissions { get; set; } = new();
    public string? TenantId      { get; set; }
    public string? TenantName    { get; set; }
    public string? BranchId      { get; set; }
    public string? BranchName    { get; set; }
    public string? HqBranchId    { get; set; }
    public bool    IsCompanyAdmin { get; set; }
}
