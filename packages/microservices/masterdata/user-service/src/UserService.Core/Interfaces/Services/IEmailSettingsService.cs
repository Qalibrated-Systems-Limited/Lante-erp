using UserService.Core.DTOs.Settings;

namespace UserService.Core.Interfaces.Services;

public interface IEmailSettingsService
{
    Task<EmailSettingsDto> GetAsync();
    Task<EmailSettingsDto> SaveAsync(SaveEmailSettingsDto dto);
    Task SendTestEmailAsync(string toEmail);

    /// <summary>Decrypted settings for the ServiceKeyAuthorize-gated internal endpoint — the only
    /// path a tenant's SMTP password ever leaves this service. Null if not configured/enabled.</summary>
    Task<InternalEmailSettingsDto?> GetForInternalAsync();
}
