using UserService.Core.DTOs.Settings;
using UserService.Core.Entities;
using UserService.Core.Interfaces.Emails;
using UserService.Core.Interfaces.Repositories;
using UserService.Core.Interfaces.Services;

namespace UserService.Infrastructure.Services;

public class EmailSettingsService(
    ITenantEmailSettingsRepository repository,
    IEmailCredentialProtector protector) : IEmailSettingsService
{
    public async Task<EmailSettingsDto> GetAsync()
    {
        var settings = await repository.GetCurrentAsync();
        return ToDto(settings);
    }

    public async Task<EmailSettingsDto> SaveAsync(SaveEmailSettingsDto dto)
    {
        var settings = await repository.GetCurrentAsync();

        var passwordEncrypted = string.IsNullOrEmpty(dto.SmtpPassword)
            ? settings?.SmtpPasswordEncrypted // keep whatever was already saved
            : protector.Encrypt(dto.SmtpPassword);

        if (settings is null)
        {
            settings = new TenantEmailSettings
            {
                IsEnabled = dto.IsEnabled,
                SmtpHost = dto.SmtpHost.Trim(),
                SmtpPort = dto.SmtpPort,
                SmtpUsername = dto.SmtpUsername.Trim(),
                SmtpPasswordEncrypted = passwordEncrypted,
                FromEmail = dto.FromEmail.Trim(),
                FromName = dto.FromName.Trim(),
            };
            await repository.CreateAsync(settings);
        }
        else
        {
            settings.IsEnabled = dto.IsEnabled;
            settings.SmtpHost = dto.SmtpHost.Trim();
            settings.SmtpPort = dto.SmtpPort;
            settings.SmtpUsername = dto.SmtpUsername.Trim();
            settings.SmtpPasswordEncrypted = passwordEncrypted;
            settings.FromEmail = dto.FromEmail.Trim();
            settings.FromName = dto.FromName.Trim();
            await repository.UpdateAsync(settings);
        }

        return ToDto(settings);
    }

    public async Task SendTestEmailAsync(string toEmail)
    {
        var settings = await repository.GetCurrentAsync()
            ?? throw new InvalidOperationException("Save your SMTP settings before sending a test email.");

        if (string.IsNullOrWhiteSpace(settings.SmtpHost))
            throw new InvalidOperationException("SMTP host is required before sending a test email.");

        var connection = new SmtpConnectionSettings(
            settings.SmtpHost,
            settings.SmtpPort,
            settings.SmtpUsername,
            string.IsNullOrEmpty(settings.SmtpPasswordEncrypted) ? string.Empty : protector.Decrypt(settings.SmtpPasswordEncrypted),
            settings.FromEmail,
            settings.FromName);

        // Deliberately not wrapped in try/catch — a failed test send must surface the real SMTP
        // error to the admin, not a generic 500, so they know what to fix.
        await RawSmtpSender.SendAsync(connection, toEmail,
            "Lante — SMTP test email",
            "<p>This is a test email confirming your custom SMTP settings are working.</p>");
    }

    public async Task<InternalEmailSettingsDto?> GetForInternalAsync()
    {
        var settings = await repository.GetCurrentAsync();
        if (settings is not { IsEnabled: true } || string.IsNullOrWhiteSpace(settings.SmtpHost))
            return null;

        return new InternalEmailSettingsDto(
            settings.SmtpHost,
            settings.SmtpPort,
            settings.SmtpUsername,
            string.IsNullOrEmpty(settings.SmtpPasswordEncrypted) ? string.Empty : protector.Decrypt(settings.SmtpPasswordEncrypted),
            settings.FromEmail,
            settings.FromName);
    }

    private static EmailSettingsDto ToDto(TenantEmailSettings? settings) => settings is null
        ? new EmailSettingsDto(false, string.Empty, 587, string.Empty, false, string.Empty, string.Empty)
        : new EmailSettingsDto(
            settings.IsEnabled,
            settings.SmtpHost,
            settings.SmtpPort,
            settings.SmtpUsername,
            !string.IsNullOrEmpty(settings.SmtpPasswordEncrypted),
            settings.FromEmail,
            settings.FromName);
}
