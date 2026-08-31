using TicketingService.Core.Entities;
using TicketingService.Core.Interfaces.Repositories;
using TicketingService.Core.Interfaces.Services;

namespace TicketingService.Api.Services;

public class AlertService(IAlertRepository repo, INotificationService notifications, ILogger<AlertService> logger) : IAlertService
{
    public Task<IEnumerable<Alert>> GetOpenAsync(string tenantId) => repo.GetOpenAsync(tenantId);

    public Task<IEnumerable<Alert>> GetHistoryAsync(string tenantId) => repo.GetHistoryAsync(tenantId);

    public Task<Alert?> GetByIdAsync(string tenantId, string id) => repo.GetByIdAsync(tenantId, id);

    public async Task CreateAsync(string tenantId, string source, string severity, string title, string message, string? ticketId = null, string? ticketTitle = null, string? assignedToUserId = null, string? requiredPermission = null)
    {
        // Background jobs (SLA, etc.) re-scan the same still-open condition on every run —
        // without this, the same breach would spawn a fresh alert every 5 minutes forever.
        //
        // This check is the fast path, not the guarantee. It is check-then-act, so at two replicas both
        // timers see "no open alert" and both proceed (#219). The partial unique index behind AddAsync
        // is what makes the outcome correct; this only avoids the pointless insert in the common case.
        if (await repo.ExistsOpenAsync(tenantId, source, title))
            return;

        var alert = new Alert
        {
            TenantId   = tenantId,
            Source     = source,
            Severity   = severity,
            Title      = title,
            Message    = message,
            TicketId   = ticketId,
            TicketTitle = ticketTitle,
            AssignedToUserId = assignedToUserId,
            RequiredPermission = requiredPermission,
        };
        // False means the index rejected it because an identical open alert already existed — the other
        // replica won. Returning here rather than falling through is the point: without it the duplicate
        // ALERT would be stopped while the duplicate EMAIL still went out, which is the half of the bug
        // people actually notice.
        if (!await repo.AddAsync(alert))
            return;

        if (!string.IsNullOrWhiteSpace(assignedToUserId))
        {
            try
            {
                await notifications.SendAsync(assignedToUserId, $"alert_{source.ToLowerInvariant()}", message, ticketId);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Alert {AlertId} created but failed to notify {UserId}", alert.Id, assignedToUserId);
            }
        }
    }

    public Task MarkSeenAsync(string tenantId, string id, string userId) => repo.MarkSeenAsync(tenantId, id, userId);

    public Task AcknowledgeAsync(string tenantId, string id, string userId) => repo.AcknowledgeAsync(tenantId, id, userId);
}
