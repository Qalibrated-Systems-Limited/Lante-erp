namespace LicenseService.Core.Interfaces.Services;

// Pushes into ticketing-service's central Alert store (internal/alerts) — schema must be passed
// explicitly since this is a service-to-service call with no JWT/schema claim of its own to
// resolve it from. Mirrors FleetService's ITicketingServiceClient.
public interface ITicketingServiceClient
{
    Task CreateAlertAsync(string tenantSchema, string source, string severity, string title, string message);
}
