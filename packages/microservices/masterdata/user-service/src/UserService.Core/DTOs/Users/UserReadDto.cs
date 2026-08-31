namespace UserService.Core.DTOs.Users;

public class UserReadDto
{
    public string Id { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string MobileNumber { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool IsFirstLogin { get; set; }
    public bool TwoFactorEnabled { get; set; }
    public string? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }
    // All dept IDs visible to this user (includes group siblings for technical/calibration/construction)
    public List<string> DepartmentIds { get; set; } = new();
    public List<string> Roles { get; set; } = new();
    public List<string> Permissions { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Tenant / branch context — populated at login
    public string? TenantId      { get; set; }
    public string? TenantName    { get; set; }
    // Postgres schema for this tenant (schema-per-tenant). Drives the `schema` JWT claim / search_path.
    public string? SchemaName    { get; set; }
    public string? BranchId      { get; set; }
    public string? BranchName    { get; set; }
    public string? HqBranchId    { get; set; }
    public bool    IsCompanyAdmin { get; set; }
}
