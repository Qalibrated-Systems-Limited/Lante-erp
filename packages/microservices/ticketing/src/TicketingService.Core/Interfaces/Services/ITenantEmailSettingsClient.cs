namespace TicketingService.Core.Interfaces.Services;

// A tenant's own SMTP settings, fetched from user-service (the system of record) — or null when
// the tenant hasn't configured/enabled custom SMTP, meaning callers should use the platform default.
public record TenantSmtpSettings(
    string SmtpHost,
    int SmtpPort,
    string SmtpUsername,
    string SmtpPassword,
    string FromEmail,
    string FromName);

public interface ITenantEmailSettingsClient
{
    Task<TenantSmtpSettings?> GetSettingsAsync();
}
