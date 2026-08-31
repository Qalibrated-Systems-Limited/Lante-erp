using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using TicketingService.Core.Interfaces.Services;

namespace TicketingService.Api.Services;

// Every outbound-email class (NotificationService, PortalEmailService, TicketNotificationService)
// used to read the "Smtp:*" config section and open its own SmtpClient inline. This centralizes
// that into one place that first tries the current tenant's own SMTP (via ITenantEmailSettingsClient)
// and falls back to the platform's shared SMTP — either because the tenant has none configured, or
// because the tenant's own server just failed to send. On fallback, the From address/name is reset
// to the platform's own — sending as the tenant's From through the platform's SMTP account is likely
// to be rejected or spam-flagged by that account's provider.
public class TenantAwareSmtpSender(ITenantEmailSettingsClient tenantClient, IConfiguration config)
{
    private sealed record SmtpProfile(string Host, int Port, string Username, string Password, string FromAddress, string FromName);

    public Task SendAsync(string toAddress, string toName, string subject, string htmlBody, ILogger logger,
        CancellationToken cancellationToken = default)
        => SendAsync(new[] { (toAddress, toName) }, subject, htmlBody, logger, cancellationToken);

    public async Task SendAsync(IEnumerable<string> toAddresses, string subject, string htmlBody, ILogger logger,
        CancellationToken cancellationToken = default)
        => await SendAsync(toAddresses.Select(a => (a, a)), subject, htmlBody, logger, cancellationToken);

    private async Task SendAsync(IEnumerable<(string Address, string Name)> recipients, string subject, string htmlBody, ILogger logger,
        CancellationToken cancellationToken)
    {
        var recipientList = recipients.ToList();
        if (recipientList.Count == 0) return;

        var tenant = await tenantClient.GetSettingsAsync();
        if (tenant != null)
        {
            try
            {
                await ConnectAndSendAsync(
                    new SmtpProfile(tenant.SmtpHost, tenant.SmtpPort, tenant.SmtpUsername, tenant.SmtpPassword, tenant.FromEmail, tenant.FromName),
                    recipientList, subject, htmlBody, cancellationToken);
                return;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Tenant's custom SMTP ({Host}) failed sending \"{Subject}\" to {Count} recipient(s); falling back to platform SMTP",
                    tenant.SmtpHost, subject, recipientList.Count);
            }
        }

        var platform = PlatformDefault();
        if (platform == null)
        {
            logger.LogInformation("SMTP disabled or not configured — skipping send of \"{Subject}\" to {Count} recipient(s)", subject, recipientList.Count);
            return;
        }

        await ConnectAndSendAsync(platform, recipientList, subject, htmlBody, cancellationToken);
    }

    private SmtpProfile? PlatformDefault()
    {
        var smtpSettings = config.GetSection("Smtp");
        var host = smtpSettings["Host"];
        var portStr = smtpSettings["Port"];
        var enabled = smtpSettings["Enabled"];

        if (enabled?.Equals("false", StringComparison.OrdinalIgnoreCase) == true) return null;
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(portStr)) return null;

        return new SmtpProfile(
            host,
            int.Parse(portStr),
            smtpSettings["Username"] ?? string.Empty,
            smtpSettings["Password"] ?? string.Empty,
            smtpSettings["From"] ?? "noreply@lante.co.ke",
            smtpSettings["FromName"] ?? "Lante");
    }

    private static async Task ConnectAndSendAsync(SmtpProfile p, List<(string Address, string Name)> recipients, string subject, string htmlBody,
        CancellationToken cancellationToken = default)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(p.FromName, p.FromAddress));
        foreach (var (address, name) in recipients)
            message.To.Add(new MailboxAddress(name, address));
        message.Subject = subject;
        message.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

        using var client = new SmtpClient();
        var secureSocket = p.Port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;
        await client.ConnectAsync(p.Host, p.Port, secureSocket, cancellationToken);

        if (!string.IsNullOrWhiteSpace(p.Username) && !string.IsNullOrWhiteSpace(p.Password))
            await client.AuthenticateAsync(p.Username, p.Password, cancellationToken);

        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(quit: true, cancellationToken);
    }
}
