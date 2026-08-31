namespace ReportingService.Core.Interfaces.Services;

// Cross-service client to ticketing — same platform convention Compliance/HSE/Fleet use instead
// of each service standing up its own SMTP/alerting stack. SendReportEmailAsync posts to
// ticketing's new internal/email endpoint (delivers ReportRecipient's ReportRun output — the
// caller embeds the download link directly in bodyHtml rather than passing a separate attachment
// concept, since ticketing has no reason to fetch/re-host another service's file). CreateAlertAsync
// mirrors Compliance's TicketingServiceClient exactly — same internal/alerts endpoint, same
// signature — for RedFlagEvaluationBackgroundService to push red-flag events.
public interface ITicketingServiceClient
{
    Task SendReportEmailAsync(string tenantSchema, string[] to, string subject, string bodyHtml);
    Task CreateAlertAsync(string tenantSchema, string source, string severity, string title, string message, string? requiredPermission = null, string? assignedToUserId = null);
}
