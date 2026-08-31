namespace UserService.Core.Entities;

// Per-tenant custom SMTP configuration — tenant-scoped like PasswordPolicy/SystemSetting (resolved
// via the request's search_path, one row per tenant). SmtpPasswordEncrypted is never returned to
// the frontend in plaintext; see IEmailCredentialProtector for the encrypt/decrypt boundary.
public class TenantEmailSettings : BaseEntity
{
    public bool IsEnabled { get; set; }
    public string SmtpHost { get; set; } = string.Empty;
    public int SmtpPort { get; set; } = 587;
    public string SmtpUsername { get; set; } = string.Empty;
    public string? SmtpPasswordEncrypted { get; set; }
    public string FromEmail { get; set; } = string.Empty;
    public string FromName { get; set; } = string.Empty;
}
