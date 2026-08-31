namespace ComplianceService.Core.Interfaces.Services;

// Posts into ticketing's shared, cross-module Alerts table (internal/alerts) — same
// platform convention HSE and Fleet use instead of owning a separate alert store.
public interface ITicketingServiceClient
{
    // requiredPermission gates who can see the alert (e.g. "compliance.read", or a tighter tier
    // like "compliance.approve" for MD/Company-Secretary-only reminders) so it doesn't land in
    // front of every alerts.read.all holder regardless of module — see ticketing's
    // AlertsController.CanSee. Null falls back to the legacy blanket alerts.read.all gate.
    // assignedToUserId additionally fires a personal Notification to that one user (see
    // AlertService.CreateAsync) — set it whenever the alert concerns a specific person (e.g. their
    // own training renewal), independent of the broader requiredPermission visibility gate.
    Task CreateAlertAsync(string tenantSchema, string source, string severity, string title, string message, string? requiredPermission = null, string? assignedToUserId = null);
}
