using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using TicketingService.Core.Entities;
using TicketingService.Core.Interfaces.Services;

namespace TicketingService.Api.Services;

public class TicketAssignmentClient(
    IHttpClientFactory httpClientFactory,
    IConfiguration config,
    ILogger<TicketAssignmentClient> logger) : ITicketAssignmentClient
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public async Task<string?> CreateAssignmentFromTicketAsync(Ticket ticket, string managerUserId, string? bearerToken)
    {
        var baseUrl = config["OperationsService:BaseUrl"]?.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            logger.LogWarning("OperationsService:BaseUrl not configured — skipping assignment creation");
            return null;
        }

        try
        {
            var client = httpClientFactory.CreateClient("OperationsService");
            if (!string.IsNullOrWhiteSpace(bearerToken))
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

            var payload = new
            {
                title = ticket.Title,
                description = ticket.Description ?? ticket.Title,
                managerId = managerUserId,
                priority = MapPriority(ticket.Priority.ToString()),
                linkedTicketId = ticket.Id,
                sourceType = "Ticket"
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await client.PostAsync($"{baseUrl}/api/v1/assignments", content);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Operations service returned {StatusCode} when creating assignment for ticket {TicketId}",
                    response.StatusCode, ticket.Id);
                return null;
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ServiceResponse>(responseJson, JsonOpts);
            string? assignmentId = null;
            if (result is { Success: true } && result.Data.ValueKind == System.Text.Json.JsonValueKind.Object)
                assignmentId = result.Data.GetProperty("id").GetString();

            logger.LogInformation("Created technician assignment {AssignmentId} from ticket {TicketId}",
                assignmentId, ticket.Id);
            return assignmentId;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create technician assignment for ticket {TicketId}", ticket.Id);
            return null;
        }
    }

    private static string MapPriority(string ticketPriority) => ticketPriority.ToLowerInvariant() switch
    {
        "critical" => "Urgent",
        "high" => "High",
        "medium" => "Normal",
        _ => "Low"
    };

    private sealed record ServiceResponse(bool Success, System.Text.Json.JsonElement Data, string? Message);
}
