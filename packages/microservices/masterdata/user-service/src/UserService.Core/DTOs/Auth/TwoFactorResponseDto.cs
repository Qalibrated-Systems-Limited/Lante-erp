namespace UserService.Core.DTOs.Auth;

public class TwoFactorResponseDto
{
    public bool Requires2FA { get; set; }
    public string SessionId { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}
