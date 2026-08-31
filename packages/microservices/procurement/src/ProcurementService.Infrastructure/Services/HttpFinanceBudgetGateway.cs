using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ProcurementService.Core.Interfaces.Services;

namespace ProcurementService.Infrastructure.Services;

/// <summary>
/// P2 — REAL Finance budget seam. Reads the Finance budgets and verifies the requisition's budget line
/// exists, is active and its annual amount covers the requested value. Mints a per-schema service token
/// (finance.read) so Finance scopes to the same tenant. Fail-open: any Finance error/unreachable →
/// available (never hard-block procurement on a Finance outage). Config-gated on FinanceService:BaseUrl.
/// </summary>
public class HttpFinanceBudgetGateway(
    IHttpClientFactory httpClientFactory,
    IConfiguration config,
    IHttpContextAccessor httpContextAccessor,
    ILogger<HttpFinanceBudgetGateway> logger) : IBudgetGateway
{
    private string? BaseUrl => config["FinanceService:BaseUrl"]?.TrimEnd('/');
    private bool Enabled => config.GetValue("Finance:Enabled", false) && !string.IsNullOrWhiteSpace(BaseUrl);

    public async Task<BudgetCheckResult> CheckAsync(string? budgetId, decimal amount, CancellationToken ct = default)
    {
        if (!Enabled || string.IsNullOrWhiteSpace(budgetId))
            return new BudgetCheckResult(true, null, "Budget check disabled — passed.");

        var schema = httpContextAccessor.HttpContext?.User.FindFirst("schema")?.Value;
        if (string.IsNullOrWhiteSpace(schema))
        {
            logger.LogWarning("Budget check for {Budget}: no tenant schema on the request — passing.", budgetId);
            return new BudgetCheckResult(true, null, "No tenant context — passed.");
        }

        try
        {
            var client = httpClientFactory.CreateClient("FinanceService");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer", await ServiceToken.MintAsync(config, httpClientFactory, schema, "system.admin", "finance.read"));
            client.DefaultRequestHeaders.Add("X-Tenant-Schema", schema);

            var resp = await client.GetAsync($"{BaseUrl}/api/v1/finance/budgets", ct);
            if (!resp.IsSuccessStatusCode)
            {
                logger.LogWarning("Finance budgets returned {Status} — passing budget {Budget}.", resp.StatusCode, budgetId);
                return new BudgetCheckResult(true, null, "Finance unavailable — passed.");
            }

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            var arr = doc.RootElement.TryGetProperty("data", out var d) && d.ValueKind == JsonValueKind.Array
                ? d : doc.RootElement.ValueKind == JsonValueKind.Array ? doc.RootElement : default;
            if (arr.ValueKind != JsonValueKind.Array)
                return new BudgetCheckResult(true, null, "Finance response unrecognised — passed.");

            foreach (var b in arr.EnumerateArray())
            {
                var id = b.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
                if (!string.Equals(id, budgetId, StringComparison.OrdinalIgnoreCase)) continue;

                var active = !b.TryGetProperty("isActive", out var ac) || ac.ValueKind != JsonValueKind.False;
                var annual = b.TryGetProperty("annualAmount", out var am) && am.TryGetDecimal(out var v) ? v : 0m;
                var name = b.TryGetProperty("departmentName", out var dn) ? dn.GetString() : null;

                if (!active)
                    return new BudgetCheckResult(false, name, "Budget line is inactive.");
                if (annual > 0 && amount > annual)
                    return new BudgetCheckResult(false, name, $"Amount ({amount:N0}) exceeds the budget annual amount ({annual:N0}).");
                return new BudgetCheckResult(true, name, "Budget available.");
            }
            return new BudgetCheckResult(false, null, "Budget line not found in Finance.");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Budget check for {Budget} failed — passing (fail-open).", budgetId);
            return new BudgetCheckResult(true, null, "Finance unreachable — passed.");
        }
    }
}
