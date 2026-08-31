using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using CrmService.Core.Interfaces.Services;

namespace CrmService.Infrastructure.Services;

/// <summary>C5 (P6) — REAL Finance seam. On deal close, creates a DRAFT AR invoice in the Finance service
/// for the contract value. Because CRM customers and Finance AR customers are separate systems, it first
/// resolves (or creates) a Finance customer matching the deal's customer, then posts the invoice tagged
/// SourceModule=CRM / SourceDocumentId=dealId. Runs in the deal-close request context; mints a per-schema
/// service token so Finance scopes to the same tenant. Degrades to a no-op result when Finance is not
/// configured, so deal close never fails on the Finance hop.</summary>
public class FinanceInvoiceGateway(
    IHttpClientFactory httpClientFactory,
    IConfiguration config,
    IHttpContextAccessor httpContextAccessor,
    ILogger<FinanceInvoiceGateway> logger) : IFinanceGateway
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };
    private string? BaseUrl => config["FinanceService:BaseUrl"]?.TrimEnd('/');

    public async Task<FinanceInvoiceResult> RaiseDealInvoiceAsync(DealInvoiceRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(BaseUrl))
        {
            logger.LogInformation("[Finance seam (disabled)] deal {Deal} → invoice {Amount} for {Customer}",
                request.DealNumber, request.Amount, request.CustomerName);
            return new FinanceInvoiceResult(true, null, "Finance integration disabled — invoice not created.");
        }

        var schema = httpContextAccessor.HttpContext?.User.FindFirst("schema")?.Value;
        if (string.IsNullOrWhiteSpace(schema))
            return new FinanceInvoiceResult(false, null, "Could not resolve tenant schema for the Finance call.");

        try
        {
            var client = httpClientFactory.CreateClient("FinanceService");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await ServiceToken.MintAsync(config, httpClientFactory, schema));
            client.DefaultRequestHeaders.Add("X-Tenant-Schema", schema);

            var financeCustomerId = await ResolveOrCreateCustomerAsync(client, request, ct);
            if (financeCustomerId is null)
                return new FinanceInvoiceResult(false, null, "Could not resolve a Finance customer for the deal.");

            var body = new
            {
                customerId = financeCustomerId,
                invoiceDate = DateTime.UtcNow,
                sourceModule = "CRM",
                sourceDocumentId = request.DealId,
                notes = $"Auto-created on close of deal {request.DealNumber}.",
                lines = new[]
                {
                    new
                    {
                        description = $"Contract value — deal {request.DealNumber}",
                        quantity = 1m,
                        unitPrice = request.Amount,
                        taxCode = "E",   // exempt: the deal value already reflects the agreed total (no double VAT)
                    },
                },
            };
            var resp = await client.PostAsync($"{BaseUrl}/api/v1/finance/invoices",
                new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"), ct);
            if (!resp.IsSuccessStatusCode)
            {
                var err = await resp.Content.ReadAsStringAsync(ct);
                logger.LogWarning("Finance invoice create returned {Status} for deal {Deal}: {Err}", resp.StatusCode, request.DealNumber, err);
                return new FinanceInvoiceResult(false, null, $"Finance rejected the invoice ({(int)resp.StatusCode}).");
            }

            var json = await resp.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            string? invoiceId = null, invoiceNo = null;
            if (doc.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object)
            {
                invoiceId = data.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
                invoiceNo = data.TryGetProperty("invoiceNo", out var noEl) ? noEl.GetString() : null;
            }
            logger.LogInformation("Created Finance draft invoice {No} ({Id}) for deal {Deal}", invoiceNo, invoiceId, request.DealNumber);
            return new FinanceInvoiceResult(true, invoiceId, $"Draft invoice {invoiceNo ?? invoiceId} created in Finance.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Finance invoice create failed for deal {Deal}", request.DealNumber);
            return new FinanceInvoiceResult(false, null, "Could not reach the Finance service.");
        }
    }

    // Resolve the Finance AR customer for a CRM customer. The strong link is the Finance customer's
    // `code` = CRM-{crmCustomerId}, so match on that FIRST (id-based, like the Operations ClientId link);
    // fall back to a case-insensitive name match for customers created before the code convention.
    private async Task<string?> ResolveOrCreateCustomerAsync(HttpClient client, DealInvoiceRequest request, CancellationToken ct)
    {
        var name = request.CustomerName?.Trim();
        var code = "CRM-" + (request.CustomerId ?? Guid.NewGuid().ToString("N"));
        code = code.Length > 20 ? code[..20] : code;

        var listResp = await client.GetAsync($"{BaseUrl}/api/v1/finance/customers", ct);
        if (listResp.IsSuccessStatusCode)
        {
            var json = await listResp.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("data", out var arr) && arr.ValueKind == JsonValueKind.Array)
            {
                var items = arr.EnumerateArray().ToList();
                // 1) strong link: match by the CRM-derived code.
                foreach (var c in items)
                    if (c.TryGetProperty("code", out var cd) && string.Equals(cd.GetString(), code, StringComparison.OrdinalIgnoreCase))
                        return c.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
                // 2) fallback: match by name (legacy customers with no CRM code).
                if (!string.IsNullOrWhiteSpace(name))
                    foreach (var c in items)
                        if (c.TryGetProperty("name", out var n) && string.Equals(n.GetString(), name, StringComparison.OrdinalIgnoreCase))
                            return c.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
            }
        }

        // Not found → create a minimal Finance customer. Code derived from the CRM customer id for traceability.
        var body = new { code, name = string.IsNullOrWhiteSpace(name) ? $"Deal {request.DealNumber} customer" : name, isActive = true };
        var createResp = await client.PostAsync($"{BaseUrl}/api/v1/finance/customers",
            new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"), ct);
        if (!createResp.IsSuccessStatusCode)
        {
            logger.LogWarning("Finance customer create returned {Status} for {Name}", createResp.StatusCode, name);
            return null;
        }
        var cjson = await createResp.Content.ReadAsStringAsync(ct);
        using var cdoc = JsonDocument.Parse(cjson);
        return cdoc.RootElement.TryGetProperty("data", out var d) && d.ValueKind == JsonValueKind.Object
            && d.TryGetProperty("id", out var cid) ? cid.GetString() : null;
    }
}
