using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using HrService.Core.Interfaces.Services;

namespace HrService.Infrastructure.Services;

/// <summary>
/// H11 — REAL CRM seam (H11-DEC-1). Reads the annual revenue target from
/// <c>GET /api/v1/dashboard/targets</c> and collected revenue from <c>GET /api/v1/dashboard/se-performance</c>,
/// with a per-schema service token.
/// <para><b>HR never stores either number.</b> CRM owns the sales target and the attainment; HR owns the
/// commission overlay that sits on top of them.</para>
/// <para><b>A failed read returns null, never zeros.</b> A commission statement computed against a silently
/// zero target would pay nothing and look like a deliberate result — the worst kind of wrong answer.</para>
/// </summary>
public class HttpCrmRevenueGateway(
    IHttpClientFactory httpClientFactory,
    IConfiguration config,
    IHttpContextAccessor httpContextAccessor,
    ILogger<HttpCrmRevenueGateway> logger) : ICrmRevenueGateway
{
    private string? BaseUrl => config["CrmService:BaseUrl"]?.TrimEnd('/');
    private string? Schema => httpContextAccessor.HttpContext?.User.FindFirst("schema")?.Value;

    public Task<List<SalesAttainmentDto>?> ListSalesAttainmentAsync(int year, CancellationToken ct = default)
        => ReadForSchemaAsync(Schema, year, ct);

    public Task<List<SalesAttainmentDto>?> ListSalesAttainmentAsync(
        string tenantSchema, int year, CancellationToken ct = default)
        => ReadForSchemaAsync(tenantSchema, year, ct);

    private async Task<List<SalesAttainmentDto>?> ReadForSchemaAsync(string? schema, int year, CancellationToken ct)
    {
        var baseUrl = BaseUrl;
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            logger.LogInformation("CRM is not configured — sales targets cannot be read.");
            return null;
        }
        if (string.IsNullOrWhiteSpace(schema))
        {
            logger.LogInformation("No tenant schema available — skipping the CRM read.");
            return null;
        }

        try
        {
            var client = httpClientFactory.CreateClient("CrmService");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer", await ServiceToken.MintAsync(config, httpClientFactory, schema, "system.admin", "crm.read.own"));
            client.DefaultRequestHeaders.Add("X-Tenant-Schema", schema);

            var targets = await ReadAsync(client, $"{baseUrl}/api/v1/dashboard/targets", ct);
            if (targets is null) return null;
            var performance = await ReadAsync(client, $"{baseUrl}/api/v1/dashboard/se-performance?period={year}", ct);
            if (performance is null) return null;

            // The annual target for THIS year. CRM labels periods "2026", "2026-Q3", "2026-07"; only the bare
            // year is the annual figure a commission plan is measured against.
            var annual = new Dictionary<string, (decimal Target, string? Name, string Currency)>();
            foreach (var el in targets)
            {
                var userId = Str(el, "employeeId");
                if (string.IsNullOrWhiteSpace(userId)) continue;
                if (!string.Equals(Str(el, "periodType"), "Annual", StringComparison.OrdinalIgnoreCase)) continue;
                if (Str(el, "periodLabel") != year.ToString()) continue;
                annual[userId!] = (Dec(el, "revenueTarget"), Str(el, "employeeName"), Str(el, "currency") ?? "KES");
            }

            var achieved = new Dictionary<string, decimal>();
            foreach (var el in performance)
            {
                var userId = Str(el, "employeeId");
                if (string.IsNullOrWhiteSpace(userId)) continue;
                achieved[userId!] = Dec(el, "revenue");
            }

            var result = annual.Select(kv => new SalesAttainmentDto(
                kv.Key, kv.Value.Name, kv.Value.Target, achieved.GetValueOrDefault(kv.Key), kv.Value.Currency)).ToList();

            logger.LogInformation("Read {Count} annual sales target(s) from CRM for {Year} in {Schema}.", result.Count, year, schema);
            return result;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "CRM sales-target read failed for {Schema} — reported as unreadable, not as zero.", schema);
            return null;
        }
    }

    private async Task<List<JsonElement>?> ReadAsync(HttpClient client, string url, CancellationToken ct)
    {
        var resp = await client.GetAsync(url, ct);
        if (!resp.IsSuccessStatusCode)
        {
            logger.LogWarning("CRM read {Url} returned {Status}.", url, resp.StatusCode);
            return null;
        }
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        if (!doc.RootElement.TryGetProperty("data", out var data)) return null;
        var array = data.ValueKind == JsonValueKind.Array ? data
            : data.ValueKind == JsonValueKind.Object && data.TryGetProperty("items", out var items) ? items
            : default;
        if (array.ValueKind != JsonValueKind.Array) return null;
        return array.EnumerateArray().Select(e => e.Clone()).ToList();
    }

    private static string? Str(JsonElement el, string name)
        => el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;

    private static decimal Dec(JsonElement el, string name)
        => el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Number ? p.GetDecimal() : 0m;
}

/// <summary>Inert CRM seam: reports "could not read", never a zero target — an unwired tenant must not have
/// commission computed against a number nobody supplied.</summary>
public class NoOpCrmRevenueGateway(ILogger<NoOpCrmRevenueGateway> logger) : ICrmRevenueGateway
{
    public Task<List<SalesAttainmentDto>?> ListSalesAttainmentAsync(int year, CancellationToken ct = default)
    {
        logger.LogInformation("crm-service not wired — sales targets cannot be read.");
        return Task.FromResult<List<SalesAttainmentDto>?>(null);
    }

    public Task<List<SalesAttainmentDto>?> ListSalesAttainmentAsync(
        string tenantSchema, int year, CancellationToken ct = default)
        => ListSalesAttainmentAsync(year, ct);
}
