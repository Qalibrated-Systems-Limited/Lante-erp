using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ProcurementService.Core.Interfaces.Services;

namespace ProcurementService.Infrastructure.Services;

/// <summary>
/// DEC-B — REAL read seam over the pre-ASR supplier masters. Finance exposes AP vendors as a flat list
/// (<c>data: []</c>); Stores exposes a paginated list (<c>data: { items: [] }</c>), so each is parsed to its
/// own envelope. Both are read-only: the seed never writes to Finance or Stores.
/// <para>Each source is independently config-gated and <b>degrades rather than fails</b> — an unreachable or
/// disabled module returns <c>Reachable=false</c> and the seed continues with the sources that answered. That
/// is safe because the seed is idempotent, so a later re-run picks up the module that was missing.</para>
/// </summary>
public class HttpSupplierSourceGateway(
    IHttpClientFactory httpClientFactory,
    IConfiguration config,
    IHttpContextAccessor httpContextAccessor,
    ILogger<HttpSupplierSourceGateway> logger) : ISupplierSourceGateway
{
    private string? FinanceUrl => config["FinanceService:BaseUrl"]?.TrimEnd('/');
    private bool FinanceEnabled => config.GetValue("Finance:Enabled", false) && !string.IsNullOrWhiteSpace(FinanceUrl);
    private string? StoresUrl => config["StoresService:BaseUrl"]?.TrimEnd('/');
    private bool StoresEnabled => config.GetValue("Stores:Enabled", false) && !string.IsNullOrWhiteSpace(StoresUrl);

    public Task<SupplierSourceResult> ListFinanceAsync(CancellationToken ct = default)
        => ReadAsync("finance", FinanceEnabled, $"{FinanceUrl}/api/v1/finance/suppliers", "finance.read", flat: true, ct);

    public Task<SupplierSourceResult> ListStoresAsync(CancellationToken ct = default)
        => ReadAsync("stores", StoresEnabled, $"{StoresUrl}/api/v1/suppliers?pageSize=500", "stores.read", flat: false, ct);

    private async Task<SupplierSourceResult> ReadAsync(
        string source, bool enabled, string url, string permission, bool flat, CancellationToken ct)
    {
        var empty = new List<ExternalSupplier>();
        if (!enabled)
            return new SupplierSourceResult(false, empty, $"{source} is not configured — skipped.");

        var schema = httpContextAccessor.HttpContext?.User.FindFirst("schema")?.Value;
        if (string.IsNullOrWhiteSpace(schema))
            return new SupplierSourceResult(false, empty, $"No tenant context for the {source} supplier read.");

        try
        {
            var client = httpClientFactory.CreateClient(source == "finance" ? "FinanceService" : "StoresService");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer", await ServiceToken.MintAsync(config, httpClientFactory, schema, "system.admin", permission));
            client.DefaultRequestHeaders.Add("X-Tenant-Schema", schema);

            var resp = await client.GetAsync(url, ct);
            var raw = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
            {
                logger.LogWarning("{Source} supplier read returned {Status}.", source, resp.StatusCode);
                return new SupplierSourceResult(false, empty, $"{source} returned {(int)resp.StatusCode} — skipped.");
            }

            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(raw) ? "{}" : raw);
            if (!doc.RootElement.TryGetProperty("data", out var data))
                return new SupplierSourceResult(true, empty, $"{source} returned no supplier data.");

            // Finance: data is the array. Stores: data.items is the array (PaginatedResult).
            var array = flat
                ? data
                : data.ValueKind == JsonValueKind.Object && data.TryGetProperty("items", out var items) ? items : default;
            if (array.ValueKind != JsonValueKind.Array)
                return new SupplierSourceResult(true, empty, $"{source} returned no supplier list.");

            var list = new List<ExternalSupplier>();
            foreach (var el in array.EnumerateArray())
            {
                var id = Str(el, "id");
                var name = Str(el, "name");
                if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name)) continue;
                list.Add(new ExternalSupplier(
                    source, id!, name!, Str(el, "kraPin"),
                    Str(el, "contactPerson"), Str(el, "phone"), Str(el, "email"), Str(el, "address"),
                    !el.TryGetProperty("isActive", out var a) || a.ValueKind != JsonValueKind.False));
            }
            logger.LogInformation("Read {Count} supplier(s) from {Source} for the ASR seed.", list.Count, source);
            return new SupplierSourceResult(true, list, $"{list.Count} supplier(s) read from {source}.");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "{Source} supplier read failed — the seed will skip that source.", source);
            return new SupplierSourceResult(false, empty, $"Could not reach {source} — skipped.");
        }
    }

    private static string? Str(JsonElement el, string name)
        => el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;
}

/// <summary>Inert source seam: reports both modules unreachable, so a seed run reports "nothing to import"
/// rather than appearing to have found no suppliers.</summary>
public class NoOpSupplierSourceGateway : ISupplierSourceGateway
{
    public Task<SupplierSourceResult> ListFinanceAsync(CancellationToken ct = default)
        => Task.FromResult(new SupplierSourceResult(false, [], "finance is not wired — skipped."));
    public Task<SupplierSourceResult> ListStoresAsync(CancellationToken ct = default)
        => Task.FromResult(new SupplierSourceResult(false, [], "stores is not wired — skipped."));
}
