using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using UserService.Core.Interfaces.Emails;

namespace UserService.Infrastructure.Services;

// The one place that actually opens an SMTP connection and sends — used by SmtpEmailService (real
// mail, platform default or tenant override) and EmailSettingsService (the "send test email"
// button), so there is exactly one send path to get right instead of two copies drifting apart.
public static class RawSmtpSender
{
    public static async Task SendAsync(SmtpConnectionSettings settings, string to, string subject, string body)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(settings.FromName, settings.FromEmail));
        message.To.Add(new MailboxAddress(to, to));
        message.Subject = subject;
        message.Body = new BodyBuilder { HtmlBody = body }.ToMessageBody();

        using var client = new SmtpClient();
        var secureSocket = settings.Port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;

        await client.ConnectAsync(settings.Host, settings.Port, secureSocket);

        if (!string.IsNullOrWhiteSpace(settings.Username) && !string.IsNullOrWhiteSpace(settings.Password))
            await client.AuthenticateAsync(settings.Username, settings.Password);

        await client.SendAsync(message);
        await client.DisconnectAsync(quit: true);
    }
}
