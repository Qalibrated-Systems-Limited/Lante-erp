using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using TicketingService.Core.Interfaces.Services;

namespace TicketingService.Api.Services;

public class OperationsServiceClient(
    IHttpClientFactory httpClientFactory,
    IConfiguration config,
    IHttpContextAccessor httpContextAccessor,
    ILogger<OperationsServiceClient> logger) : IOperationsServiceClient
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    // The anonymous portal has no JWT, so operations can't resolve the tenant from a token. Forward
    // the gateway-injected tenant headers on this request so the ops interceptor points search_path
    // at the right tenant schema for the internal (service-key) ingest call.
    private void ForwardTenantHeaders(HttpClient client)
    {
        var req = httpContextAccessor.HttpContext?.Request;
        if (req == null) return;
        var schema = req.Headers["X-Tenant-Schema"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(schema))
            client.DefaultRequestHeaders.Add("X-Tenant-Schema", schema);
        var tenantId = req.Headers["X-Tenant-Id"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(tenantId))
            client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId);
    }

    public async Task<string?> CreateAssignmentFromServiceRequestAsync(
        CreateSrAssignmentPayload payload,
        string? bearerToken)
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

            var body = new
            {
                title                 = payload.Title,
                description           = payload.Description,
                sourceType            = "Ticket",
                linkedTicketId        = payload.LinkedTicketId,
                serviceRequestId      = payload.ServiceRequestId,
                serviceRequestDataJson= payload.ServiceRequestDataJson,
                departmentId          = payload.DepartmentId,
                locationName          = payload.LocationName,
                locationAddress       = payload.LocationAddress,
                locationLatitude      = payload.LocationLatitude,
                locationLongitude     = payload.LocationLongitude,
                priority              = payload.Priority,
                natureOfVisit         = payload.NatureOfVisit,
                serviceType           = payload.ServiceType,
                deadline              = payload.Deadline,
                technicianIds         = payload.TechnicianIds,
                technicianNames       = payload.TechnicianNames,
            };

            var json    = JsonSerializer.Serialize(body);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await client.PostAsync($"{baseUrl}/api/v1/assignments", content);

            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync();
                logger.LogWarning("OperationsService returned {Status} creating assignment for SR {SrId}: {Error}",
                    response.StatusCode, payload.ServiceRequestId, err);
                return null;
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<ServiceResponse>(responseJson, JsonOpts);
            string? assignmentId = null;
            if (result is { Success: true } && result.Data.ValueKind == JsonValueKind.Object)
                assignmentId = result.Data.GetProperty("id").GetString();

            logger.LogInformation("Created assignment {AssignmentId} from SR {SrId}", assignmentId, payload.ServiceRequestId);
            return assignmentId;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create assignment from SR {SrId}", payload.ServiceRequestId);
            return null;
        }
    }

    public async Task<string?> PushServiceRequestAsync(IngestServiceRequestPayload payload)
    {
        var baseUrl = config["OperationsService:BaseUrl"]?.TrimEnd('/');
        var serviceKey = config["InternalServices:ServiceKey"];
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            logger.LogWarning("OperationsService:BaseUrl not configured — skipping SR push to operations");
            return null;
        }

        try
        {
            var client = httpClientFactory.CreateClient("OperationsService");
            if (!string.IsNullOrWhiteSpace(serviceKey))
                client.DefaultRequestHeaders.Add("X-Internal-Key", serviceKey);
            ForwardTenantHeaders(client);

            var json    = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await client.PostAsync($"{baseUrl}/internal/service-requests", content);

            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync();
                logger.LogWarning("OperationsService returned {Status} ingesting SR {Ref}: {Error}",
                    response.StatusCode, payload.ReferenceNumber, err);
                return null;
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(responseJson);
            var reference = doc.RootElement.TryGetProperty("data", out var data)
                            && data.ValueKind == JsonValueKind.Object
                            && data.TryGetProperty("referenceNumber", out var refEl)
                ? refEl.GetString()
                : payload.ReferenceNumber;

            logger.LogInformation("Pushed SR {Ref} to operations", reference);
            return reference;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to push SR {Ref} to operations", payload.ReferenceNumber);
            return null;
        }
    }

    public async Task<ProxyResponse> GetServiceRequestTrackingAsync(string reference)
    {
        var baseUrl = config["OperationsService:BaseUrl"]?.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl))
            return new ProxyResponse(503, "{\"message\":\"Operations service not configured.\"}", false);

        try
        {
            var client = httpClientFactory.CreateClient("OperationsService");
            var serviceKey = config["InternalServices:ServiceKey"];
            if (!string.IsNullOrWhiteSpace(serviceKey))
                client.DefaultRequestHeaders.Add("X-Internal-Key", serviceKey);
            ForwardTenantHeaders(client);

            var response = await client.GetAsync(
                $"{baseUrl}/internal/service-requests/track/{Uri.EscapeDataString(reference)}");
            var body = await response.Content.ReadAsStringAsync();
            return new ProxyResponse((int)response.StatusCode, body, true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to proxy SR tracking for {Ref}", reference);
            return new ProxyResponse(502, "{\"message\":\"Could not reach the service request service.\"}", false);
        }
    }

    public async Task<ProxyResponse> SubmitServiceRequestSignatureAsync(string reference, string signatureData)
    {
        var baseUrl = config["OperationsService:BaseUrl"]?.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl))
            return new ProxyResponse(503, "{\"message\":\"Operations service not configured.\"}", false);

        try
        {
            var client = httpClientFactory.CreateClient("OperationsService");
            var serviceKey = config["InternalServices:ServiceKey"];
            if (!string.IsNullOrWhiteSpace(serviceKey))
                client.DefaultRequestHeaders.Add("X-Internal-Key", serviceKey);
            ForwardTenantHeaders(client);

            var json    = JsonSerializer.Serialize(new { signatureData });
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await client.PostAsync(
                $"{baseUrl}/internal/service-requests/{Uri.EscapeDataString(reference)}/signature", content);
            var body = await response.Content.ReadAsStringAsync();
            return new ProxyResponse((int)response.StatusCode, body, true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to proxy SR signature for {Ref}", reference);
            return new ProxyResponse(502, "{\"message\":\"Could not reach the service request service.\"}", false);
        }
    }

    private sealed record ServiceResponse(bool Success, JsonElement Data, string? Message);
}
