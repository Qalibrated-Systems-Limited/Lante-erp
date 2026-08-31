using System.Text;
using System.Text.Json;
using FleetService.Core.Interfaces;

namespace FleetService.Api.Services;

public class TicketingServiceClient(
    IHttpClientFactory httpClientFactory,
    IConfiguration config,
    ILogger<TicketingServiceClient> logger) : ITicketingServiceClient
{
    public async Task NotifyTicketAsync(string ticketId, string tripId, FleetWorkUpdateType updateType, string? notes = null)
    {
        var baseUrl = config["TicketingService:BaseUrl"]?.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            logger.LogWarning("TicketingService:BaseUrl not configured — skipping ticket callback");
            return;
        }

        var serviceKey = config["InternalServices:ServiceKey"];
        if (string.IsNullOrWhiteSpace(serviceKey))
        {
            logger.LogWarning("InternalServices:ServiceKey not configured — skipping ticket callback");
            return;
        }

        try
        {
            var client = httpClientFactory.CreateClient("TicketingService");
            client.DefaultRequestHeaders.Add("X-Service-Key", serviceKey);

            var payload = new
            {
                ticketId,
                source = "FleetService",
                externalReferenceId = tripId,
                updateType = updateType.ToString(),
                notes
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await client.PostAsync($"{baseUrl}/api/v1/internal/work-update", content);

            if (!response.IsSuccessStatusCode)
                logger.LogWarning("TicketingService returned {StatusCode} for ticket {TicketId} callback", response.StatusCode, ticketId);
            else
                logger.LogInformation("Ticket {TicketId} updated via callback ({UpdateType})", ticketId, updateType);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to notify TicketingService for ticket {TicketId}", ticketId);
        }
    }

    public async Task CreateAlertAsync(string tenantSchema, string source, string severity, string title, string message,
        string? requiredPermission = null)
    {
        var baseUrl = config["TicketingService:BaseUrl"]?.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            logger.LogWarning("TicketingService:BaseUrl not configured — skipping alert creation");
            return;
        }

        var serviceKey = config["InternalServices:ServiceKey"];
        if (string.IsNullOrWhiteSpace(serviceKey))
        {
            logger.LogWarning("InternalServices:ServiceKey not configured — skipping alert creation");
            return;
        }

        try
        {
            var client = httpClientFactory.CreateClient("TicketingService");
            // internal/alerts is gated by [ServiceKeyAuthorize], which checks X-Internal-Key —
            // NOT X-Service-Key (that header name is specific to internal/work-update above,
            // which checks it manually rather than via the shared attribute).
            client.DefaultRequestHeaders.Add("X-Internal-Key", serviceKey);
            client.DefaultRequestHeaders.Add("X-Tenant-Schema", tenantSchema);

            var payload = new { source, severity, title, message, requiredPermission };
            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await client.PostAsync($"{baseUrl}/internal/alerts", content);

            if (!response.IsSuccessStatusCode)
                logger.LogWarning("TicketingService returned {StatusCode} creating alert \"{Title}\" for schema {Schema}", response.StatusCode, title, tenantSchema);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create alert \"{Title}\" for schema {Schema}", title, tenantSchema);
        }
    }

    public async Task NotifyUserAsync(string tenantSchema, string userId, string type, string message)
    {
        var baseUrl = config["TicketingService:BaseUrl"]?.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            logger.LogWarning("TicketingService:BaseUrl not configured — skipping notification for user {UserId}", userId);
            return;
        }

        var serviceKey = config["InternalServices:ServiceKey"];
        if (string.IsNullOrWhiteSpace(serviceKey))
        {
            logger.LogWarning("InternalServices:ServiceKey not configured — skipping notification for user {UserId}", userId);
            return;
        }

        try
        {
            var client = httpClientFactory.CreateClient("TicketingService");
            // internal/notifications is gated by [ServiceKeyAuthorize] — same X-Internal-Key/
            // X-Tenant-Schema pattern as internal/alerts above.
            client.DefaultRequestHeaders.Add("X-Internal-Key", serviceKey);
            client.DefaultRequestHeaders.Add("X-Tenant-Schema", tenantSchema);

            var payload = new { userId, type, message };
            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await client.PostAsync($"{baseUrl}/internal/notifications", content);

            if (!response.IsSuccessStatusCode)
                logger.LogWarning("TicketingService returned {StatusCode} sending notification to user {UserId}", response.StatusCode, userId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send notification to user {UserId}", userId);
        }
    }

    public async Task SendEmailAsync(string[] to, string subject, string bodyHtml)
    {
        var baseUrl = config["TicketingService:BaseUrl"]?.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            logger.LogWarning("TicketingService:BaseUrl not configured — skipping email \"{Subject}\"", subject);
            return;
        }

        var serviceKey = config["InternalServices:ServiceKey"];
        if (string.IsNullOrWhiteSpace(serviceKey))
        {
            logger.LogWarning("InternalServices:ServiceKey not configured — skipping email \"{Subject}\"", subject);
            return;
        }

        try
        {
            var client = httpClientFactory.CreateClient("TicketingService");
            client.DefaultRequestHeaders.Add("X-Internal-Key", serviceKey);

            var payload = new { to, subject, bodyHtml };
            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await client.PostAsync($"{baseUrl}/internal/email", content);

            if (!response.IsSuccessStatusCode)
                logger.LogWarning("TicketingService returned {StatusCode} sending email \"{Subject}\"", response.StatusCode, subject);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send email \"{Subject}\"", subject);
        }
    }
}
