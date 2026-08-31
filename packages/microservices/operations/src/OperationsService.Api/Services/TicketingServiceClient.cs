using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using OperationsService.Core.Interfaces.Services;

namespace OperationsService.Api.Services;

public class TicketingServiceClient(
    IHttpClientFactory httpClientFactory,
    IConfiguration config,
    IHttpContextAccessor httpContextAccessor,
    ILogger<TicketingServiceClient> logger) : ITicketingServiceClient
{
    // Some callbacks are fired from the service layer (e.g. SR status sync), which has no bearer
    // token to hand in. Fall back to the ambient request's Authorization header / tenant header so
    // the receiving ticketing service resolves the correct tenant schema.
    private string? AmbientBearer =>
        httpContextAccessor.HttpContext?.Request.Headers.Authorization
            .FirstOrDefault()?.Replace("Bearer ", "", StringComparison.OrdinalIgnoreCase);

    private string? AmbientTenantSchema =>
        httpContextAccessor.HttpContext?.Request.Headers["X-Tenant-Schema"].FirstOrDefault();
    public async Task NotifyWorkUpdateAsync(string ticketId, string assignmentId, string updateType, string? notes, string? bearerToken)
    {
        var baseUrl = config["TicketingService:BaseUrl"]?.TrimEnd('/');
        var serviceKey = config["InternalServices:ServiceKey"];

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            logger.LogWarning("TicketingService:BaseUrl not configured — skipping work update callback");
            return;
        }

        try
        {
            var client = httpClientFactory.CreateClient("TicketingService");
            if (!string.IsNullOrWhiteSpace(bearerToken))
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
            if (!string.IsNullOrWhiteSpace(serviceKey))
                client.DefaultRequestHeaders.Add("X-Service-Key", serviceKey);

            var payload = new
            {
                ticketId,
                source = "OperationsService",
                externalReferenceId = assignmentId,
                updateType,
                notes
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await client.PostAsync($"{baseUrl}/api/v1/internal/work-update", content);

            if (!response.IsSuccessStatusCode)
                logger.LogWarning("Ticketing work-update returned {StatusCode} for ticket {TicketId}", response.StatusCode, ticketId);
            else
                logger.LogInformation("Notified ticketing service: ticket {TicketId} → {UpdateType}", ticketId, updateType);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to notify ticketing service for ticket {TicketId}", ticketId);
        }
    }

    public async Task NotifyServiceRequestStatusAsync(string ticketId, string referenceNumber, string status, string? note, string? bearerToken)
    {
        if (string.IsNullOrWhiteSpace(ticketId)) return;

        var baseUrl = config["TicketingService:BaseUrl"]?.TrimEnd('/');
        var serviceKey = config["InternalServices:ServiceKey"];
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            logger.LogWarning("TicketingService:BaseUrl not configured — skipping SR status sync");
            return;
        }

        try
        {
            var client = httpClientFactory.CreateClient("TicketingService");
            var token = string.IsNullOrWhiteSpace(bearerToken) ? AmbientBearer : bearerToken;
            if (!string.IsNullOrWhiteSpace(token))
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            if (!string.IsNullOrWhiteSpace(serviceKey))
                client.DefaultRequestHeaders.Add("X-Service-Key", serviceKey);
            var schema = AmbientTenantSchema;
            if (!string.IsNullOrWhiteSpace(schema))
                client.DefaultRequestHeaders.Add("X-Tenant-Schema", schema);

            var payload = new { ticketId, referenceNumber, status, note };
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var response = await client.PostAsync($"{baseUrl}/api/v1/internal/service-request-status", content);

            if (!response.IsSuccessStatusCode)
                logger.LogWarning("Ticketing SR-status sync returned {Status} for ticket {TicketId}", response.StatusCode, ticketId);
            else
                logger.LogInformation("Synced SR {Ref} status {Status} to ticket {TicketId}", referenceNumber, status, ticketId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to sync SR {Ref} status to ticket {TicketId}", referenceNumber, ticketId);
        }
    }

    public async Task RecordCertificateOnSrAsync(string serviceRequestId, string certificateNumber, DateTime issuedAt, string? bearerToken)
    {
        var baseUrl = config["TicketingService:BaseUrl"]?.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl)) return;

        try
        {
            var client = httpClientFactory.CreateClient("TicketingService");
            if (!string.IsNullOrWhiteSpace(bearerToken))
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

            var payload = new { certificateNumber, certificateIssuedAt = issuedAt };
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var response = await client.PostAsync($"{baseUrl}/api/v1/service-requests/{serviceRequestId}/certificate", content);

            if (!response.IsSuccessStatusCode)
                logger.LogWarning("Ticketing certificate callback returned {Status} for SR {SrId}", response.StatusCode, serviceRequestId);
            else
                logger.LogInformation("Recorded certificate {Cert} on SR {SrId}", certificateNumber, serviceRequestId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to record certificate on SR {SrId}", serviceRequestId);
        }
    }
}
