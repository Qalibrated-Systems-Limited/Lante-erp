using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using CrmService.Core.Interfaces.Services;

namespace CrmService.Infrastructure.Services;

/// <summary>C13 (P14) — real Finance-service reader. Because the alert sweep runs in a background scope
/// (no inbound JWT to forward), this mints a short-lived service token carrying the tenant <c>schema</c>
/// claim so Finance's interceptor points search_path at the right tenant. Signed with the shared
/// <c>JwtSettings:SecretKey</c> (same secret/issuer/audience as every service). Degrades to empty lists
/// when Finance is not configured or unreachable so the sweep never throws.</summary>
public class FinanceReadClient(
    IHttpClientFactory httpClientFactory,
    IConfiguration config,
    ILogger<FinanceReadClient> logger) : IFinanceReadClient
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private string? BaseUrl => config["FinanceService:BaseUrl"]?.TrimEnd('/');
    public bool IsConfigured => !string.IsNullOrWhiteSpace(BaseUrl);

    public Task<List<FinanceArInvoice>> GetArInvoicesAsync(string schema, CancellationToken ct = default)
        => GetListAsync<FinanceArInvoice>("/api/v1/finance/invoices", schema, ct);

    public Task<List<FinanceDebtorAging>> GetDebtorAgingAsync(string schema, CancellationToken ct = default)
        => GetListAsync<FinanceDebtorAging>("/api/v1/finance/debtors/aging", schema, ct);

    public Task<List<FinanceSupplierInvoice>> GetSupplierInvoicesAsync(string schema, CancellationToken ct = default)
        => GetListAsync<FinanceSupplierInvoice>("/api/v1/finance/supplier-invoices", schema, ct);

    public Task<List<FinanceVoucher>> GetVouchersAsync(string schema, CancellationToken ct = default)
        => GetListAsync<FinanceVoucher>("/api/v1/finance/vouchers", schema, ct);

    private async Task<List<T>> GetListAsync<T>(string path, string schema, CancellationToken ct)
    {
        if (!IsConfigured)
        {
            logger.LogDebug("Finance not configured (FinanceService:BaseUrl unset) — skipping {Path}", path);
            return [];
        }
        try
        {
            var client = httpClientFactory.CreateClient("FinanceService");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await ServiceToken.MintAsync(config, httpClientFactory, schema));
            // Belt-and-braces: the JWT schema claim wins, but forward the header too.
            client.DefaultRequestHeaders.Add("X-Tenant-Schema", schema);

            var resp = await client.GetAsync($"{BaseUrl}{path}", ct);
            if (!resp.IsSuccessStatusCode)
            {
                logger.LogWarning("Finance {Path} returned {Status} for schema {Schema}", path, resp.StatusCode, schema);
                return [];
            }
            var json = await resp.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
                return [];
            return JsonSerializer.Deserialize<List<T>>(data.GetRawText(), JsonOpts) ?? [];
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Finance {Path} read failed for schema {Schema}", path, schema);
            return [];
        }
    }

}
