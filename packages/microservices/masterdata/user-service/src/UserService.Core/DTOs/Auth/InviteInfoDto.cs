namespace UserService.Core.DTOs.Auth;

/// <summary>What the accept-invite page shows before the user sets their password.</summary>
public class InviteInfoDto
{
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? CompanyName { get; set; }
}
