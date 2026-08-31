using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using UserService.Core.Interfaces.Emails;
using UserService.Core.Interfaces.Repositories;

namespace UserService.Infrastructure.Services;

public class SmtpEmailService(
    IConfiguration configuration,
    ITenantEmailSettingsRepository tenantEmailSettings,
    IEmailCredentialProtector protector,
    ILogger<SmtpEmailService> logger) : IEmailService
{
    public async Task SendEmailAsync(string to, string subject, string body)
    {
        var tenantSettings = await tenantEmailSettings.GetCurrentAsync();

        if (tenantSettings is { IsEnabled: true } && !string.IsNullOrWhiteSpace(tenantSettings.SmtpHost))
        {
            try
            {
                var custom = new SmtpConnectionSettings(
                    tenantSettings.SmtpHost,
                    tenantSettings.SmtpPort,
                    tenantSettings.SmtpUsername,
                    string.IsNullOrEmpty(tenantSettings.SmtpPasswordEncrypted)
                        ? string.Empty
                        : protector.Decrypt(tenantSettings.SmtpPasswordEncrypted),
                    tenantSettings.FromEmail,
                    tenantSettings.FromName);

                await RawSmtpSender.SendAsync(custom, to, subject, body);
                logger.LogInformation("Email sent to {To} via tenant's own SMTP ({Host})", to, tenantSettings.SmtpHost);
                return;
            }
            catch (Exception ex)
            {
                // Falls through to the platform default below rather than failing the send outright
                // — a tenant's misconfigured SMTP shouldn't block invites/notifications from going out.
                logger.LogWarning(ex,
                    "Tenant's custom SMTP ({Host}) failed sending to {To}; falling back to platform SMTP",
                    tenantSettings.SmtpHost, to);
            }
        }

        var platformDefault = new SmtpConnectionSettings(
            configuration["Email:SmtpHost"] ?? "smtp.gmail.com",
            int.Parse(configuration["Email:SmtpPort"] ?? "587"),
            configuration["Email:SmtpUsername"] ?? string.Empty,
            configuration["Email:SmtpPassword"] ?? string.Empty,
            configuration["Email:FromEmail"] ?? "noreply@lante.com",
            configuration["Email:FromName"] ?? "Lante");

        await RawSmtpSender.SendAsync(platformDefault, to, subject, body);
        logger.LogInformation("Email sent to {To} with subject '{Subject}'", to, subject);
    }
}
