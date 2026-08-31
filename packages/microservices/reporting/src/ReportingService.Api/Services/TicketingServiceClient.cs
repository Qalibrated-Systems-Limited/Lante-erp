using System.Text;
using System.Text.Json;
using ReportingService.Core.Interfaces.Services;

namespace ReportingService.Api.Services;

public class TicketingServiceClient(
    IHttpClientFactory httpClientFactory,
    IConfiguration config,
    ILogger<TicketingServiceClient> logger) : ITicketingServiceClient
{
    public async Task SendReportEmailAsync(string tenantSchema, string[] to, string subject, string bodyHtml)
    {
        var baseUrl = config["TicketingService:BaseUrl"]?.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            logger.LogWarning("TicketingService:BaseUrl not configured — skipping report email delivery");
            return;
        }

        var serviceKey = config["InternalServices:ServiceKey"];
        if (string.IsNullOrWhiteSpace(serviceKey))
        {
            logger.LogWarning("InternalServices:ServiceKey not configured — skipping report email delivery");
            return;
        }

        try
        {
            var client = httpClientFactory.CreateClient("TicketingService");
            client.DefaultRequestHeaders.Add("X-Internal-Key", serviceKey);
            client.DefaultRequestHeaders.Add("X-Tenant-Schema", tenantSchema);

            var payload = new { to, subject, bodyHtml };
            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await client.PostAsync($"{baseUrl}/internal/email", content);

            if (!response.IsSuccessStatusCode)
                logger.LogWarning("TicketingService returned {StatusCode} sending report email \"{Subject}\" for schema {Schema}", response.StatusCode, subject, tenantSchema);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send report email \"{Subject}\" for schema {Schema}", subject, tenantSchema);
        }
    }

    public async Task CreateAlertAsync(string tenantSchema, string source, string severity, string title, string message, string? requiredPermission = null, string? assignedToUserId = null)
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
            client.DefaultRequestHeaders.Add("X-Internal-Key", serviceKey);
            client.DefaultRequestHeaders.Add("X-Tenant-Schema", tenantSchema);

            var payload = new { source, severity, title, message, requiredPermission, assignedToUserId };
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
}
