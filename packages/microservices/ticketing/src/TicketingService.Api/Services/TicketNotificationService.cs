


using System.Net.Http.Headers;
using System.Text.Json;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using TicketingService.Core.Interfaces.Services;

namespace TicketingService.Api.Services;

public class TicketNotificationService(
    IHttpClientFactory httpClientFactory,
    IHttpContextAccessor httpContextAccessor,
    IConfiguration config,
    TenantAwareSmtpSender sender,
    ILogger<TicketNotificationService> logger) : ITicketNotificationService
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public async Task SendDepartmentAssignmentNotificationsAsync(
        string ticketId,
        string ticketTitle,
        string departmentId,
        string assignedByUserId,
        string? notes,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var targets = await GetNotificationTargetsAsync(departmentId, cancellationToken);
            if (!targets.Any())
            {
                logger.LogInformation("No notification targets found for department {DepartmentId}", departmentId);
                return;
            }

            foreach (var target in targets)
            {
                await SendNotificationEmailAsync(target.Name, target.Email, target.Reason,
                    ticketId, ticketTitle, departmentId, notes, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send department assignment notifications for ticket {TicketId}", ticketId);
        }
    }

    private async Task<IEnumerable<NotificationTarget>> GetNotificationTargetsAsync(
        string departmentId, CancellationToken cancellationToken)
    {
        var userServiceUrl = config["UserService:BaseUrl"]?.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(userServiceUrl))
        {
            logger.LogWarning("UserService:BaseUrl not configured — skipping notification target lookup");
            return [];
        }

        var token = httpContextAccessor.HttpContext?.Request.Headers.Authorization
            .FirstOrDefault()?.Replace("Bearer ", "", StringComparison.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(token))
        {
            logger.LogWarning("No JWT token available for user-service call — skipping notification targets");
            return [];
        }

        using var client = httpClientFactory.CreateClient("UserService");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var url = $"{userServiceUrl}/api/v1/users/notification-targets?departmentId={Uri.EscapeDataString(departmentId)}";
        var response = await client.GetAsync(url, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("User-service returned {StatusCode} for notification targets", response.StatusCode);
            return [];
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var wrapper = JsonSerializer.Deserialize<UserServiceResponse<IEnumerable<NotificationTarget>>>(json, JsonOpts);
        return wrapper?.Data ?? [];
    }

    private async Task SendNotificationEmailAsync(
        string toName, string toEmail, string reason,
        string ticketId, string ticketTitle, string departmentId,
        string? notes, CancellationToken cancellationToken)
    {
        try
        {
            var html = BuildNotificationHtml(toName, reason, ticketId, ticketTitle, departmentId, notes);
            await sender.SendAsync(toEmail, toName, $"[Lante] Ticket Assigned to Department — {ticketTitle}", html, logger, cancellationToken);

            logger.LogInformation("Department assignment notification sent to {Email} ({Reason})", toEmail, reason);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send notification email to {Email}", toEmail);
        }
    }

    private static string BuildNotificationHtml(
        string name, string reason,
        string ticketId, string ticketTitle,
        string departmentId, string? notes)
    {
        var escapedName = System.Net.WebUtility.HtmlEncode(name);
        var escapedTitle = System.Net.WebUtility.HtmlEncode(ticketTitle);
        var escapedNotes = notes != null ? System.Net.WebUtility.HtmlEncode(notes) : string.Empty;
        var escapedReason = System.Net.WebUtility.HtmlEncode(reason);

        var notesRow = string.IsNullOrWhiteSpace(escapedNotes) ? string.Empty : $"""
            <tr style="border-top:1px solid #e5e7eb;">
              <td style="padding:10px 16px;font-size:12px;font-weight:600;color:#6b7280;">Notes</td>
              <td style="padding:10px 16px;font-size:14px;color:#374151;line-height:1.6;">{escapedNotes}</td>
            </tr>
            """;

        return $"""
            <!DOCTYPE html>
            <html lang="en">
            <head>
              <meta charset="UTF-8" />
              <meta name="viewport" content="width=device-width, initial-scale=1.0" />
              <title>Ticket Department Assignment</title>
            </head>
            <body style="margin:0;padding:0;background:#f4f4f5;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,sans-serif;">
              <table width="100%" cellpadding="0" cellspacing="0" style="background:#f4f4f5;padding:32px 16px;">
                <tr>
                  <td align="center">
                    <table width="600" cellpadding="0" cellspacing="0" style="max-width:600px;width:100%;background:#ffffff;border-radius:12px;overflow:hidden;box-shadow:0 2px 8px rgba(0,0,0,.08);">
                      <tr>
                        <td style="background:#0f1e45;padding:28px 32px;">
                          <div style="display:inline-block;width:36px;height:36px;background:#f59e0b;border-radius:8px;text-align:center;line-height:36px;font-weight:900;color:#fff;font-size:18px;margin-right:10px;vertical-align:middle;">L</div>
                          <span style="color:#ffffff;font-size:17px;font-weight:700;vertical-align:middle;">Lante</span>
                        </td>
                      </tr>
                      <tr>
                        <td style="padding:32px 32px 24px;border-bottom:1px solid #f0f0f0;">
                          <p style="margin:0 0 8px;font-size:22px;font-weight:700;color:#0f1e45;">Ticket Assigned to Your Department</p>
                          <p style="margin:0;font-size:15px;color:#6b7280;line-height:1.6;">
                            Hello {escapedName}, a ticket has been assigned to your department. You are receiving this as: <strong>{escapedReason}</strong>.
                          </p>
                        </td>
                      </tr>
                      <tr>
                        <td style="padding:24px 32px;">
                          <p style="margin:0 0 14px;font-size:13px;font-weight:600;color:#6b7280;text-transform:uppercase;letter-spacing:.05em;">Ticket Details</p>
                          <table width="100%" cellpadding="0" cellspacing="0" style="border:1px solid #e5e7eb;border-radius:10px;overflow:hidden;">
                            <tr style="background:#f9fafb;">
                              <td style="padding:10px 16px;font-size:12px;font-weight:600;color:#6b7280;width:120px;">Ticket ID</td>
                              <td style="padding:10px 16px;font-size:14px;color:#111827;font-family:'Courier New',monospace;">{System.Net.WebUtility.HtmlEncode(ticketId)}</td>
                            </tr>
                            <tr style="border-top:1px solid #e5e7eb;">
                              <td style="padding:10px 16px;font-size:12px;font-weight:600;color:#6b7280;">Title</td>
                              <td style="padding:10px 16px;font-size:14px;color:#111827;font-weight:600;">{escapedTitle}</td>
                            </tr>
                            {notesRow}
                          </table>
                        </td>
                      </tr>
                      <tr>
                        <td style="background:#f9fafb;border-top:1px solid #e5e7eb;padding:20px 32px;text-align:center;">
                          <p style="margin:0;font-size:12px;color:#9ca3af;">
                            Lante &copy; {DateTime.UtcNow.Year} &mdash;
                            <a href="mailto:info@lante.co.ke" style="color:#f59e0b;text-decoration:none;">info@lante.co.ke</a>
                          </p>
                          <p style="margin:6px 0 0;font-size:11px;color:#d1d5db;">This is an automated notification. Please do not reply to this email.</p>
                        </td>
                      </tr>
                    </table>
                  </td>
                </tr>
              </table>
            </body>
            </html>
            """;
    }

    private sealed record NotificationTarget(string UserId, string Name, string Email, string Reason);
    private sealed record UserServiceResponse<T>(bool Success, T? Data, string? Message);
}
