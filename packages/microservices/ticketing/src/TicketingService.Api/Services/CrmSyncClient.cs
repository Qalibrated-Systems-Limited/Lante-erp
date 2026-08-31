using System.Text;
using System.Text.Json;
using TicketingService.Core.Integrations;

namespace TicketingService.Api.Services;

/// <summary>
/// D8-2 / D8-3 — REAL CRM (Module 6) sink. Posts closed-ticket customer interactions and resolved
/// complaints to the crm-service internal ingest endpoints. Service-to-service auth via the shared
/// <c>X-Internal-Key</c>; the tenant is carried on <c>X-Tenant-Schema</c> from the payload (the D8 records
/// already capture the tenant schema, so this works from background/workflow contexts with no request
/// JWT). Never throws — integration failures must not break ticket handling. Enabled via
/// <c>Integrations:Crm:Enabled</c> + <c>CrmService:BaseUrl</c>.
/// </summary>
public class CrmSyncClient(
    IHttpClientFactory httpClientFactory,
    IConfiguration config,
    ILogger<CrmSyncClient> logger) : ICrmSync
{
    private string? BaseUrl => config["CrmService:BaseUrl"]?.TrimEnd('/');

    public bool IsEnabled =>
        config.GetSection("Integrations:Crm")["Enabled"]?.Equals("true", StringComparison.OrdinalIgnoreCase) == true
        && !string.IsNullOrWhiteSpace(BaseUrl);

    public Task RecordCustomerInteractionAsync(CustomerInteraction interaction, CancellationToken ct = default) =>
        PostAsync("internal/crm/customer-interactions", interaction.TenantSchema, new
        {
            customerId = interaction.CustomerId,
            customerName = interaction.CustomerName,
            ticketId = interaction.TicketId,
            ticketReference = interaction.TicketReference,
            interactionType = interaction.InteractionType,
            summary = interaction.Summary,
            occurredAt = interaction.OccurredAt,
            handledByUserId = interaction.HandledByUserId,
        }, interaction.TicketReference, ct);

    public Task RecordComplaintClosureAsync(ComplaintClosure closure, CancellationToken ct = default) =>
        PostAsync("internal/crm/complaint-closures", closure.TenantSchema, new
        {
            customerId = closure.CustomerId,
            customerName = closure.CustomerName,
            ticketId = closure.TicketId,
            ticketReference = closure.TicketReference,
            rootCause = closure.RootCause,
            preventiveAction = closure.PreventiveAction,
            satisfactionMet = closure.SatisfactionMet,
            closedAt = closure.ClosedAt,
        }, closure.TicketReference, ct);

    private async Task PostAsync(string path, string tenantSchema, object body, string reference, CancellationToken ct)
    {
        if (!IsEnabled) return;
        var serviceKey = config["InternalServices:ServiceKey"];
        try
        {
            var client = httpClientFactory.CreateClient("CrmService");
            if (!string.IsNullOrWhiteSpace(serviceKey))
                client.DefaultRequestHeaders.Add("X-Internal-Key", serviceKey);
            if (!string.IsNullOrWhiteSpace(tenantSchema))
                client.DefaultRequestHeaders.Add("X-Tenant-Schema", tenantSchema);

            var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
            var resp = await client.PostAsync($"{BaseUrl}/{path}", content, ct);
            if (!resp.IsSuccessStatusCode)
                logger.LogWarning("CRM sync {Path} returned {Status} for {Ref}", path, resp.StatusCode, reference);
            else
                logger.LogInformation("CRM sync {Path} ok for {Ref}", path, reference);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "CRM sync {Path} failed for {Ref} (best-effort)", path, reference);
        }
    }
}
