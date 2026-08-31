using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using TicketingService.Core.Entities;
using TicketingService.Core.Interfaces.Services;

namespace TicketingService.Api.Services;

public class FleetServiceClient(
    IHttpClientFactory httpClientFactory,
    IConfiguration config,
    ILogger<FleetServiceClient> logger) : IFleetServiceClient
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public async Task<string?> CreateTripFromTicketAsync(Ticket ticket, string requestedByUserId, string? bearerToken)
    {
        var baseUrl = config["FleetService:BaseUrl"]?.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            logger.LogWarning("FleetService:BaseUrl not configured — skipping trip creation");
            return null;
        }

        try
        {
            var client = httpClientFactory.CreateClient("FleetService");
            if (!string.IsNullOrWhiteSpace(bearerToken))
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

            var payload = new
            {
                purpose = ticket.Title,
                notes = ticket.Description ?? ticket.Title,
                requestedByUserId,
                linkedTicketId = ticket.Id,
                scheduledDeparture = DateTime.UtcNow.AddHours(2)
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await client.PostAsync($"{baseUrl}/api/v1/trips", content);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("FleetService returned {StatusCode} when creating trip for ticket {TicketId}",
                    response.StatusCode, ticket.Id);
                return null;
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ServiceResponse>(responseJson, JsonOpts);
            string? tripId = null;
            if (result is { Success: true } && result.Data.ValueKind == System.Text.Json.JsonValueKind.Object)
                tripId = result.Data.GetProperty("id").GetString();

            logger.LogInformation("Created fleet trip {TripId} from ticket {TicketId}", tripId, ticket.Id);
            return tripId;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create fleet trip for ticket {TicketId}", ticket.Id);
            return null;
        }
    }

    private sealed record ServiceResponse(bool Success, System.Text.Json.JsonElement Data, string? Message);
}
