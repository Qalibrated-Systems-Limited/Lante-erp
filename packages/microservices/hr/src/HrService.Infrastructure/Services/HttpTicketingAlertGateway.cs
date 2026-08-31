using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using HrService.Core.Interfaces.Services;

namespace HrService.Infrastructure.Services;

/// <summary>
/// H2 — REAL alert seam. Posts to ticketing's <c>internal/alerts</c> with the ServiceKey pattern
/// (<c>X-Internal-Key</c> + <c>X-Tenant-Schema</c>, no JWT minting needed), mirroring the compliance statutory
/// calendar so HR alerts surface in the same inbox staff already use.
/// <para>Best-effort by design: the probation review and contract-renewal rows are the durable record, so a
/// ticketing outage is logged and the sweep carries on rather than failing the milestone.</para>
/// </summary>
public class HttpTicketingAlertGateway(
    IHttpClientFactory httpClientFactory,
    IConfiguration config,
    ILogger<HttpTicketingAlertGateway> logger) : IHrAlertGateway
{
    public async Task CreateAlertAsync(
        string tenantSchema, string source, string severity, string title, string message,
        string? requiredPermission = null, string? assignedToUserId = null, CancellationToken ct = default)
    {
        var baseUrl = config["TicketingService:BaseUrl"]?.TrimEnd('/');
        var serviceKey = config["InternalServices:ServiceKey"];
        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(serviceKey))
        {
            logger.LogInformation("Ticketing alerts not configured — skipping HR alert \"{Title}\".", title);
            return;
        }

        try
        {
            var client = httpClientFactory.CreateClient("TicketingService");
            client.DefaultRequestHeaders.Add("X-Internal-Key", serviceKey);
            client.DefaultRequestHeaders.Add("X-Tenant-Schema", tenantSchema);

            var payload = new { source, severity, title, message, requiredPermission, assignedToUserId };
            var resp = await client.PostAsync($"{baseUrl}/internal/alerts",
                new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"), ct);
            if (!resp.IsSuccessStatusCode)
                logger.LogWarning("Ticketing returned {Status} creating HR alert \"{Title}\" for {Schema}.", resp.StatusCode, title, tenantSchema);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to raise HR alert \"{Title}\" for {Schema} (best-effort).", title, tenantSchema);
        }
    }
}

/// <summary>Inert alert seam: logs the intended notification. The milestone records still stand, so HR sees the
/// work in its own queues even with no notification channel wired.</summary>
public class NoOpHrAlertGateway(ILogger<NoOpHrAlertGateway> logger) : IHrAlertGateway
{
    public Task CreateAlertAsync(
        string tenantSchema, string source, string severity, string title, string message,
        string? requiredPermission = null, string? assignedToUserId = null, CancellationToken ct = default)
    {
        logger.LogInformation("HR alert (not delivered — no channel wired): [{Severity}] {Title}", severity, title);
        return Task.CompletedTask;
    }
}
