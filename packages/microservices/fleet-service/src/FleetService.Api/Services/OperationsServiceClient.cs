using System.Text.Json;
using FleetService.Core.Interfaces;

namespace FleetService.Api.Services;

public class OperationsServiceClient(
    IHttpClientFactory httpClientFactory,
    IConfiguration config,
    ILogger<OperationsServiceClient> logger) : IOperationsServiceClient
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public async Task<AssignmentSummaryDto?> GetAssignmentAsync(string tenantSchema, string assignmentId)
    {
        var baseUrl = config["OperationsService:BaseUrl"]?.TrimEnd('/');
        var serviceKey = config["InternalServices:ServiceKey"];
        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(serviceKey))
        {
            logger.LogWarning("OperationsService:BaseUrl or InternalServices:ServiceKey not configured — skipping assignment lookup for {AssignmentId}", assignmentId);
            return null;
        }

        try
        {
            var client = httpClientFactory.CreateClient("OperationsService");
            client.DefaultRequestHeaders.Add("X-Internal-Key", serviceKey);
            client.DefaultRequestHeaders.Add("X-Tenant-Schema", tenantSchema);

            var response = await client.GetAsync($"{baseUrl}/internal/assignments/{assignmentId}");
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("OperationsService returned {StatusCode} looking up assignment {AssignmentId}", response.StatusCode, assignmentId);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            var wrapper = JsonSerializer.Deserialize<AssignmentResponse>(json, JsonOpts);
            return wrapper?.Data;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to look up assignment {AssignmentId} from OperationsService", assignmentId);
            return null;
        }
    }

    private sealed record AssignmentResponse(bool Success, AssignmentSummaryDto? Data);
}
