using System.Net.Http.Headers;
using System.Text.Json;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using TicketingService.Core.Entities;
using TicketingService.Core.Interfaces.Repositories;
using TicketingService.Core.Interfaces.Services;

namespace TicketingService.Api.Services;

public class NotificationService(
    INotificationRepository repo,
    ITicketWatcherRepository watcherRepo,
    IHttpClientFactory httpClientFactory,
    IHttpContextAccessor httpContextAccessor,
    IConfiguration config,
    ISmsSender smsSender,
    TenantAwareSmtpSender smtpSender,
    ILogger<NotificationService> logger) : INotificationService
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    // ── Public API ─────────────────────────────────────────────────────────────

    public Task<IEnumerable<Notification>> GetForUserAsync(string userId, bool unreadOnly = false)
        => repo.GetForUserAsync(userId, unreadOnly);

    public Task<int> GetUnreadCountAsync(string userId)
        => repo.GetUnreadCountAsync(userId);

    public Task<Notification?> GetByIdAsync(string id)
        => repo.GetByIdAsync(id);

    public Task MarkReadAsync(string notificationId, string userId)
        => repo.MarkReadAsync(notificationId, userId);

    public Task MarkUnreadAsync(string notificationId, string userId)
        => repo.MarkUnreadAsync(notificationId, userId);

    public Task MarkAllReadAsync(string userId)
        => repo.MarkAllReadAsync(userId);

    // ── Notification triggers ──────────────────────────────────────────────────

    public async Task NotifyAssignedAsync(Ticket ticket, string assignedToUserId, string assignedByUserId)
    {
        if (string.IsNullOrWhiteSpace(assignedToUserId)) return;
        if (assignedToUserId == assignedByUserId) return; // don't notify when assigning to yourself

        var n = new Notification
        {
            UserId      = assignedToUserId,
            Type        = "assigned",
            Title       = "Ticket assigned to you",
            Message     = $"You have been assigned ticket: {ticket.Title}",
            TicketId    = ticket.Id,
            TicketTitle = ticket.Title,
        };
        await repo.AddAsync(n);
        await SendEmailAsync(assignedToUserId, n.Title, n.Message, ticket.Id);
        await SendSmsAsync(assignedToUserId, n.Message);
    }

    public async Task NotifyStatusChangedAsync(Ticket ticket, string oldStatus, string newStatus, string changedByUserId)
    {
        var targets = new HashSet<string>();
        if (!string.IsNullOrWhiteSpace(ticket.CreatedByUserId)) targets.Add(ticket.CreatedByUserId);
        if (!string.IsNullOrWhiteSpace(ticket.AssignedToUserId)) targets.Add(ticket.AssignedToUserId);
        targets.Remove(changedByUserId); // don't notify the person who made the change

        if (!targets.Any()) return;

        var notifications = targets.Select(userId => new Notification
        {
            UserId      = userId,
            Type        = "status_changed",
            Title       = $"Ticket status changed to {newStatus}",
            Message     = $"Ticket \"{ticket.Title}\" status changed from {oldStatus} to {newStatus}.",
            TicketId    = ticket.Id,
            TicketTitle = ticket.Title,
        }).ToList();

        await repo.AddRangeAsync(notifications);

        foreach (var n in notifications)
        {
            await SendEmailAsync(n.UserId, n.Title, n.Message, ticket.Id);
            await SendSmsAsync(n.UserId, n.Message);
        }
    }

    public async Task NotifyCommentAddedAsync(Ticket ticket, string commentAuthorUserId, string commentPreview)
    {
        var targets = new HashSet<string>();
        if (!string.IsNullOrWhiteSpace(ticket.CreatedByUserId)) targets.Add(ticket.CreatedByUserId);
        if (!string.IsNullOrWhiteSpace(ticket.AssignedToUserId)) targets.Add(ticket.AssignedToUserId);
        targets.Remove(commentAuthorUserId);

        if (!targets.Any()) return;

        var preview = commentPreview.Length > 100 ? commentPreview[..100] + "…" : commentPreview;
        var notifications = targets.Select(userId => new Notification
        {
            UserId      = userId,
            Type        = "comment_added",
            Title       = "New comment on your ticket",
            Message     = $"New comment on \"{ticket.Title}\": {preview}",
            TicketId    = ticket.Id,
            TicketTitle = ticket.Title,
        }).ToList();

        await repo.AddRangeAsync(notifications);

        foreach (var n in notifications)
        {
            await SendEmailAsync(n.UserId, n.Title, n.Message, ticket.Id);
            await SendSmsAsync(n.UserId, n.Message);
        }
    }

    public async Task NotifyEscalatedAsync(Ticket ticket, string escalatedByUserId)
    {
        var targets = new HashSet<string>();
        if (!string.IsNullOrWhiteSpace(ticket.AssignedToUserId)) targets.Add(ticket.AssignedToUserId);
        if (!string.IsNullOrWhiteSpace(ticket.CreatedByUserId)) targets.Add(ticket.CreatedByUserId);
        targets.Remove(escalatedByUserId);

        if (!targets.Any()) return;

        var notifications = targets.Select(userId => new Notification
        {
            UserId      = userId,
            Type        = "escalated",
            Title       = "Ticket escalated",
            Message     = $"Ticket \"{ticket.Title}\" has been escalated.",
            TicketId    = ticket.Id,
            TicketTitle = ticket.Title,
        }).ToList();

        await repo.AddRangeAsync(notifications);
        foreach (var n in notifications)
        {
            await SendEmailAsync(n.UserId, n.Title, n.Message, ticket.Id);
            await SendSmsAsync(n.UserId, n.Message);
        }
    }

    public async Task NotifyResolvedAsync(Ticket ticket, string resolvedByUserId)
    {
        if (string.IsNullOrWhiteSpace(ticket.CreatedByUserId)) return;
        if (ticket.CreatedByUserId == resolvedByUserId) return;

        var n = new Notification
        {
            UserId      = ticket.CreatedByUserId,
            Type        = "resolved",
            Title       = "Your ticket has been resolved",
            Message     = $"Ticket \"{ticket.Title}\" has been marked as resolved.",
            TicketId    = ticket.Id,
            TicketTitle = ticket.Title,
        };
        await repo.AddAsync(n);
        await SendEmailAsync(ticket.CreatedByUserId, n.Title, n.Message, ticket.Id);
        await SendSmsAsync(ticket.CreatedByUserId, n.Message);
    }

    public async Task<string> SendAsync(string userId, string type, string message, string? ticketId = null)
    {
        if (string.IsNullOrWhiteSpace(userId)) return string.Empty;

        var n = new Notification
        {
            UserId      = userId,
            Type        = type,
            Title       = type,
            Message     = message,
            // NULL, not empty string — TicketId has a convention-based FK to Tickets.Id via the
            // Ticket navigation property; "" isn't null so it still gets FK-checked and fails
            // since no ticket has id="".
            TicketId    = string.IsNullOrWhiteSpace(ticketId) ? null : ticketId,
            TicketTitle = string.Empty
        };
        await repo.AddAsync(n);

        if (!string.IsNullOrWhiteSpace(ticketId))
            await SendEmailAsync(userId, type, message, ticketId);

        // Every notification gets an SMS attempt, regardless of whether it's ticket-linked —
        // unlike the email above (which needs a ticketId to build its "View Ticket" link),
        // SMS is just the plain message text.
        await SendSmsAsync(userId, message);

        return n.Id;
    }

    public async Task NotifyWatchersAsync(Ticket ticket, string eventType, string message, string changedByUserId)
    {
        var watchers = (await watcherRepo.GetByTicketIdAsync(ticket.Id)).ToList();
        var targets = watchers
            .Select(w => w.UserId)
            .Where(uid => uid != changedByUserId)
            .Distinct()
            .ToList();

        if (targets.Count == 0) return;

        var title = $"Watched ticket update: {eventType}";
        var notifications = targets.Select(uid => new Notification
        {
            UserId      = uid,
            Type        = "watcher_update",
            Title       = title,
            Message     = message,
            TicketId    = ticket.Id,
            TicketTitle = ticket.Title,
        }).ToList();

        await repo.AddRangeAsync(notifications);

        foreach (var n in notifications)
        {
            await SendEmailAsync(n.UserId, title, message, ticket.Id);
            await SendSmsAsync(n.UserId, message);
        }
    }

    // ── SMS ────────────────────────────────────────────────────────────────────

    private async Task SendSmsAsync(string userId, string message)
    {
        if (!smsSender.IsEnabled) return;

        try
        {
            var userInfo = await GetUserInfoAsync(userId);
            if (userInfo == null || string.IsNullOrWhiteSpace(userInfo.MobileNumber)) return;

            await smsSender.SendAsync(userInfo.MobileNumber, $"Lante: {message}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send notification SMS to user {UserId}", userId);
        }
    }

    // ── Email ──────────────────────────────────────────────────────────────────

    private async Task SendEmailAsync(string userId, string subject, string bodyText, string ticketId)
    {
        try
        {
            var userInfo = await GetUserInfoAsync(userId);
            if (userInfo == null) return;

            var html = BuildHtml(userInfo.Name, subject, bodyText, ticketId);
            await smtpSender.SendAsync(userInfo.Email, userInfo.Name, $"[Lante] {subject}", html, logger);

            logger.LogInformation("Notification email sent to {Email} — {Subject}", userInfo.Email, subject);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send notification email to user {UserId}", userId);
        }
    }

    // A JWT to forward exists when this runs synchronously inside a real user's own API request
    // (e.g. assigning a ticket). It does NOT exist when this runs off an internal, service-to-service
    // call (e.g. InternalNotificationsController, hit by fleet-service with no end-user token at all) —
    // that path falls back to the shared internal service-key instead, same as CreateAlertAsync's callers.
    private async Task<UserInfo?> GetUserInfoAsync(string userId)
    {
        var baseUrl = config["UserService:BaseUrl"]?.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl)) return null;

        var token = httpContextAccessor.HttpContext?.Request.Headers.Authorization
            .FirstOrDefault()?.Replace("Bearer ", "", StringComparison.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(token))
        {
            try
            {
                using var client = httpClientFactory.CreateClient("UserService");
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var resp = await client.GetAsync($"{baseUrl}/api/v1/users/{userId}");
                if (resp.IsSuccessStatusCode)
                {
                    var json = await resp.Content.ReadAsStringAsync();
                    var wrapper = JsonSerializer.Deserialize<UserServiceResponse>(json, JsonOpts);
                    if (wrapper?.Data != null) return wrapper.Data;
                }
            }
            catch { /* fall through to the internal path below */ }
        }

        var serviceKey = config["InternalServices:ServiceKey"];
        if (string.IsNullOrWhiteSpace(serviceKey)) return null;

        try
        {
            using var client = httpClientFactory.CreateClient("UserService");
            client.DefaultRequestHeaders.Add("X-Internal-Key", serviceKey);

            var schema = CurrentTenantSchema();
            if (!string.IsNullOrWhiteSpace(schema))
                client.DefaultRequestHeaders.Add("X-Tenant-Schema", schema);

            var resp = await client.GetAsync($"{baseUrl}/internal/users/{userId}");
            if (!resp.IsSuccessStatusCode) return null;

            var json = await resp.Content.ReadAsStringAsync();
            var wrapper = JsonSerializer.Deserialize<UserServiceResponse>(json, JsonOpts);
            return wrapper?.Data;
        }
        catch
        {
            return null;
        }
    }

    // Mirrors TenantDbConnectionInterceptor.Resolve() — needed to forward the right tenant schema
    // on the internal-lookup path above, which has no JWT of its own to derive it from.
    private string? CurrentTenantSchema()
    {
        var ctx = httpContextAccessor.HttpContext;
        if (ctx == null) return null;
        return ctx.User.FindFirst("schema")?.Value ?? ctx.Request.Headers["X-Tenant-Schema"].FirstOrDefault();
    }

    private static string BuildHtml(string name, string subject, string message, string ticketId)
    {
        var escapedName = System.Net.WebUtility.HtmlEncode(name);
        var escapedMessage = System.Net.WebUtility.HtmlEncode(message);
        var escapedSubject = System.Net.WebUtility.HtmlEncode(subject);

        return $"""
            <!DOCTYPE html>
            <html lang="en">
            <head><meta charset="UTF-8"/><title>{escapedSubject}</title></head>
            <body style="margin:0;padding:0;background:#f4f4f5;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,sans-serif;">
              <table width="100%" cellpadding="0" cellspacing="0" style="background:#f4f4f5;padding:32px 16px;">
                <tr><td align="center">
                  <table width="600" cellpadding="0" cellspacing="0" style="max-width:600px;width:100%;background:#fff;border-radius:12px;overflow:hidden;box-shadow:0 2px 8px rgba(0,0,0,.08);">
                    <tr>
                      <td style="background:#0f1e45;padding:24px 32px;">
                        <div style="display:inline-block;width:32px;height:32px;background:#f59e0b;border-radius:8px;text-align:center;line-height:32px;font-weight:900;color:#fff;font-size:16px;margin-right:10px;vertical-align:middle;">L</div>
                        <span style="color:#fff;font-size:16px;font-weight:700;vertical-align:middle;">Lante</span>
                      </td>
                    </tr>
                    <tr>
                      <td style="padding:28px 32px 20px;">
                        <p style="margin:0 0 6px;font-size:20px;font-weight:700;color:#0f1e45;">{escapedSubject}</p>
                        <p style="margin:0;font-size:14px;color:#6b7280;">Hello {escapedName},</p>
                        <p style="margin:12px 0 0;font-size:15px;color:#374151;line-height:1.6;">{escapedMessage}</p>
                      </td>
                    </tr>
                    <tr>
                      <td style="padding:0 32px 32px;text-align:center;">
                        <a href="http://localhost:3000/modules/ticketing/{ticketId}"
                           style="display:inline-block;padding:11px 24px;background:#f59e0b;color:#fff;font-size:14px;font-weight:700;text-decoration:none;border-radius:8px;">
                          View Ticket →
                        </a>
                      </td>
                    </tr>
                    <tr>
                      <td style="background:#f9fafb;border-top:1px solid #e5e7eb;padding:16px 32px;text-align:center;">
                        <p style="margin:0;font-size:11px;color:#9ca3af;">Lante &copy; {DateTime.UtcNow.Year} — This is an automated notification.</p>
                      </td>
                    </tr>
                  </table>
                </td></tr>
              </table>
            </body>
            </html>
            """;
    }

    private sealed record UserInfo(string Id, string Email, string FirstName, string LastName, string MobileNumber)
    {
        public string Name => $"{FirstName} {LastName}".Trim();
    }
    private sealed record UserServiceResponse(bool Success, UserInfo? Data);
}
