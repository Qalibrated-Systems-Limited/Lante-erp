namespace FleetService.Core.Interfaces;

public enum FleetWorkUpdateType { WorkStarted, WorkCompleted, WorkCancelled }

public interface ITicketingServiceClient
{
    Task NotifyTicketAsync(string ticketId, string tripId, FleetWorkUpdateType updateType, string? notes = null);

    // Pushes into ticketing-service's central Alert store (internal/alerts) — schema must be
    // passed explicitly since this is a service-to-service call with no JWT/schema claim of its
    // own to resolve it from. requiredPermission scopes who can see the alert (via ticketing's
    // AlertsController.CanSee) — pass e.g. "fleet.write" so only Fleet Manager/Staff/Admin see it,
    // instead of it falling back to the tenant-wide "alerts.read.all" permission.
    Task CreateAlertAsync(string tenantSchema, string source, string severity, string title, string message,
        string? requiredPermission = null);

    // Pushes an in-app notification for one specific user (internal/notifications) — e.g. telling
    // the original requester their dispatch was approved. Same no-JWT/explicit-schema situation as
    // CreateAlertAsync above.
    Task NotifyUserAsync(string tenantSchema, string userId, string type, string message);

    // Sends a plain email through ticketing-service's central SMTP client (internal/email) rather
    // than fleet-service standing up its own. No tenant scoping needed — email doesn't touch the DB.
    Task SendEmailAsync(string[] to, string subject, string bodyHtml);
}
