using System.ComponentModel.DataAnnotations;

namespace UserService.Core.DTOs.Auth;

public class AcceptInviteDto
{
    [Required] public string Token { get; set; } = string.Empty;
    [Required] public string NewPassword { get; set; } = string.Empty;
    [Required] public string ConfirmPassword { get; set; } = string.Empty;
}
