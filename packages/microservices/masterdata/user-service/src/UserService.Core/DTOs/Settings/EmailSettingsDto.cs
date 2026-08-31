namespace UserService.Core.DTOs.Settings;

// Returned to the frontend — never carries the password, only whether one is set, so the UI can
// show "•••••••• (set)" instead of round-tripping a secret through every GET.
public record EmailSettingsDto(
    bool IsEnabled,
    string SmtpHost,
    int SmtpPort,
    string SmtpUsername,
    bool HasPassword,
    string FromEmail,
    string FromName);

// Sent from the frontend to save settings. Password is optional: null/empty means "keep the
// currently saved password" (so re-saving other fields doesn't force re-entering it every time).
public record SaveEmailSettingsDto(
    bool IsEnabled,
    string SmtpHost,
    int SmtpPort,
    string SmtpUsername,
    string? SmtpPassword,
    string FromEmail,
    string FromName);

public record SendTestEmailDto(string ToEmail);

// Returned to internal callers (other services) only — the one place the decrypted password
// legitimately leaves user-service, over the ServiceKeyAuthorize-gated internal endpoint.
public record InternalEmailSettingsDto(
    string SmtpHost,
    int SmtpPort,
    string SmtpUsername,
    string SmtpPassword,
    string FromEmail,
    string FromName);
