namespace HSEService.Core.Interfaces.Services;

// Posts into ticketing's shared, cross-module Alerts table (internal/alerts) — the platform
// convention every business service uses instead of owning its own alert store (see
// FleetService's LicenseExpiryBackgroundService / TicketingServiceClient for the reference impl).
public interface ITicketingServiceClient
{
    // assignedToUserId personalizes the alert to one person (also fires a Notification, per
    // ticketing's AlertService.CreateAsync). requiredPermission independently gates who ELSE can
    // see it (e.g. "hse.read") — ticketing's AlertsController.CanSee shows an alert to the
    // assignee OR any holder of RequiredPermission, so both can be set together: the specific
    // employee gets personally notified while Safety/Construction managers keep oversight
    // visibility. Alerts with neither (e.g. a statutory inspection has no individual owner) fall
    // back to the legacy blanket alerts.read.all gate, which is unseeded on any role today —
    // always set requiredPermission for HSE alerts unless there's a deliberate reason not to.
    Task CreateAlertAsync(string tenantSchema, string source, string severity, string title, string message, string? assignedToUserId = null, string? requiredPermission = null);
}
