namespace UserService.Core.Interfaces.Emails;

// Plain connection parameters for a single SMTP send — shared shape between the platform default
// (read from IConfiguration), a tenant's saved custom settings, and the "send test email" flow, so
// all three paths funnel through the one actual SMTP client in RawSmtpSender.
public record SmtpConnectionSettings(
    string Host,
    int Port,
    string Username,
    string Password,
    string FromEmail,
    string FromName);
