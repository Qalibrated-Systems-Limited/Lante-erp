using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ProcurementService.Core.Interfaces.Services;

namespace ProcurementService.Infrastructure.Services;

/// <summary>
/// P7 — REAL Finance FX seam. Reads the tenant's currency table (GET finance <c>/currencies</c>) so
/// international costs convert at the rate Finance holds. Rates are base-per-foreign-unit (KES per USD), so
/// callers multiply. Mints a per-schema service token (finance.read).
/// <para><b>Fail-closed:</b> an unreachable Finance yields null, never a fallback rate — the caller then
/// demands an explicit rate from the user. A silently wrong FX rate would corrupt every landed cost.</para>
/// </summary>
public class HttpFinanceCurrencyGateway(
    IHttpClientFactory httpClientFactory,
    IConfiguration config,
    IHttpContextAccessor httpContextAccessor,
    ILogger<HttpFinanceCurrencyGateway> logger) : ICurrencyGateway
{
    private string? BaseUrl => config["FinanceService:BaseUrl"]?.TrimEnd('/');
    private bool Enabled => config.GetValue("Finance:Enabled", false) && !string.IsNullOrWhiteSpace(BaseUrl);

    public async Task<decimal?> GetRateAsync(string currencyCode, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(currencyCode)) return null;
        var all = await ListAsync(ct);
        foreach (var (code, _, rate) in all)
            if (string.Equals(code, currencyCode, StringComparison.OrdinalIgnoreCase))
                return rate > 0 ? rate : null;
        return null;
    }

    public async Task<List<(string Code, string? Name, decimal Rate)>> ListAsync(CancellationToken ct = default)
    {
        var result = new List<(string, string?, decimal)>();
        if (!Enabled) return result;

        var schema = httpContextAccessor.HttpContext?.User.FindFirst("schema")?.Value;
        if (string.IsNullOrWhiteSpace(schema)) return result;

        try
        {
            var client = httpClientFactory.CreateClient("FinanceService");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer", await ServiceToken.MintAsync(config, httpClientFactory, schema, "system.admin", "finance.read"));
            client.DefaultRequestHeaders.Add("X-Tenant-Schema", schema);

            var resp = await client.GetAsync($"{BaseUrl}/api/v1/finance/currencies", ct);
            if (!resp.IsSuccessStatusCode)
            {
                logger.LogWarning("Finance currencies returned {Status}.", resp.StatusCode);
                return result;
            }

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
                return result;

            foreach (var el in data.EnumerateArray())
            {
                var code = el.TryGetProperty("code", out var c) && c.ValueKind == JsonValueKind.String ? c.GetString() : null;
                if (string.IsNullOrWhiteSpace(code)) continue;
                var active = !el.TryGetProperty("isActive", out var a) || a.ValueKind != JsonValueKind.False;
                if (!active) continue;
                var rate = el.TryGetProperty("exchangeRate", out var r) && r.ValueKind == JsonValueKind.Number ? r.GetDecimal() : 0m;
                var name = el.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString() : null;
                result.Add((code!, name, rate));
            }
            return result;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Finance currency lookup failed (fail-closed — caller must supply a rate).");
            return result;
        }
    }
}

/// <summary>Inert FX seam for deployments without Finance: reports no rates, so callers require an explicit
/// rate rather than converting at a guess.</summary>
public class NoOpCurrencyGateway : ICurrencyGateway
{
    public Task<decimal?> GetRateAsync(string currencyCode, CancellationToken ct = default) => Task.FromResult<decimal?>(null);
    public Task<List<(string Code, string? Name, decimal Rate)>> ListAsync(CancellationToken ct = default)
        => Task.FromResult(new List<(string, string?, decimal)>());
}
