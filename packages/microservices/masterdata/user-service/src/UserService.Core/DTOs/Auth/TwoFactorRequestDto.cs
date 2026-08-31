using System.ComponentModel.DataAnnotations;

namespace UserService.Core.DTOs.Auth;

public class TwoFactorRequestDto
{
    [Required]
    public string SessionId { get; set; } = string.Empty;

    [Required]
    public string Code { get; set; } = string.Empty;
}
