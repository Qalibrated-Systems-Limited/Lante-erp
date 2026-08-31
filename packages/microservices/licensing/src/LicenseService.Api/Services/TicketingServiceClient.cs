using System.Text;
using System.Text.Json;
using LicenseService.Core.Interfaces.Services;

namespace LicenseService.Api.Services;

public class TicketingServiceClient(
    IHttpClientFactory httpClientFactory,
    IConfiguration config,
    ILogger<TicketingServiceClient> logger) : ITicketingServiceClient
{
    public async Task CreateAlertAsync(string tenantSchema, string source, string severity, string title, string message)
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

            var payload = new { source, severity, title, message };
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
