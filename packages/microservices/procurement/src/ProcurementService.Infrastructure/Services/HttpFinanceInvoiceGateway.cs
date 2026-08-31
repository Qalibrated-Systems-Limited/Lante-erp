using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ProcurementService.Core.Interfaces.Services;

namespace ProcurementService.Infrastructure.Services;

/// <summary>
/// P6 — REAL Finance payables read seam. Lists Finance's supplier invoices and picks the one recorded
/// against this LPO (finance.SupplierInvoice.LpoReference). Mints a per-schema service token (finance.read).
/// <para><b>Fail-closed</b>, unlike the P2 budget and P4 journal seams: this feeds a payment gate, so a
/// Finance outage is reported as "not reachable" and the matching engine refuses to run rather than
/// concluding the invoice is missing.</para>
/// </summary>
public class HttpFinanceInvoiceGateway(
    IHttpClientFactory httpClientFactory,
    IConfiguration config,
    IHttpContextAccessor httpContextAccessor,
    ILogger<HttpFinanceInvoiceGateway> logger) : IInvoiceGateway
{
    private string? BaseUrl => config["FinanceService:BaseUrl"]?.TrimEnd('/');
    private bool Enabled => config.GetValue("Finance:Enabled", false) && !string.IsNullOrWhiteSpace(BaseUrl);

    public async Task<InvoiceLookup> FindByLpoAsync(string poNumber, CancellationToken ct = default)
    {
        if (!Enabled)
            return new InvoiceLookup(false, null, "Finance is not configured, so the supplier invoice cannot be verified.");

        var schema = httpContextAccessor.HttpContext?.User.FindFirst("schema")?.Value;
        if (string.IsNullOrWhiteSpace(schema))
            return new InvoiceLookup(false, null, "No tenant context for the Finance invoice lookup.");

        try
        {
            var client = httpClientFactory.CreateClient("FinanceService");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer", await ServiceToken.MintAsync(config, httpClientFactory, schema, "system.admin", "finance.read"));
            client.DefaultRequestHeaders.Add("X-Tenant-Schema", schema);

            var resp = await client.GetAsync($"{BaseUrl}/api/v1/finance/supplier-invoices", ct);
            var raw = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
            {
                var reason = Str(JsonDocument.Parse(SafeJson(raw)).RootElement, "message");
                logger.LogWarning("Finance supplier-invoice list returned {Status} while matching {Po}: {Reason}", resp.StatusCode, poNumber, reason);
                return new InvoiceLookup(false, null,
                    reason is null
                        ? $"Finance rejected the invoice lookup ({(int)resp.StatusCode})."
                        : $"Finance rejected the invoice lookup: {reason}");
            }

            using var doc = JsonDocument.Parse(raw);
            if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
                return new InvoiceLookup(true, null, "Finance returned no payables.");

            foreach (var el in data.EnumerateArray())
            {
                var lpo = Str(el, "lpoReference");
                if (!string.Equals(lpo?.Trim(), poNumber, StringComparison.OrdinalIgnoreCase)) continue;

                var inv = new SupplierInvoiceRef(
                    Str(el, "id") ?? string.Empty,
                    Str(el, "supplierInvoiceNo") ?? Str(el, "internalNo") ?? string.Empty,
                    Str(el, "supplierId"),
                    Str(el, "supplierName"),
                    Dec(el, "subtotal"),
                    Dec(el, "total"),
                    Dec(el, "balance"),
                    Str(el, "status") ?? string.Empty,
                    Str(el, "matchStatus") ?? string.Empty);
                logger.LogInformation("Matched LPO {Po} to Finance invoice {Inv}.", poNumber, inv.InvoiceNo);
                return new InvoiceLookup(true, inv, $"Invoice {inv.InvoiceNo} found.");
            }

            return new InvoiceLookup(true, null, $"Finance holds no supplier invoice referencing {poNumber}.");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Finance invoice lookup failed for LPO {Po} (fail-closed).", poNumber);
            return new InvoiceLookup(false, null, "Could not reach Finance for the supplier invoice.");
        }
    }

    public async Task<bool> SetMatchStatusAsync(string supplierInvoiceId, string status, string? note, CancellationToken ct = default)
    {
        if (!Enabled) return false;
        var schema = httpContextAccessor.HttpContext?.User.FindFirst("schema")?.Value;
        if (string.IsNullOrWhiteSpace(schema)) return false;

        try
        {
            var client = httpClientFactory.CreateClient("FinanceService");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer", await ServiceToken.MintAsync(config, httpClientFactory, schema, "system.admin", "finance.write"));
            client.DefaultRequestHeaders.Add("X-Tenant-Schema", schema);

            var body = JsonSerializer.Serialize(new { status, note });
            var resp = await client.PostAsync(
                $"{BaseUrl}/api/v1/finance/supplier-invoices/{supplierInvoiceId}/match-status",
                new StringContent(body, Encoding.UTF8, "application/json"), ct);
            if (resp.IsSuccessStatusCode) return true;

            logger.LogWarning("Finance match-status write-back returned {Status} for invoice {Inv}: {Reason}",
                resp.StatusCode, supplierInvoiceId,
                Str(JsonDocument.Parse(SafeJson(await resp.Content.ReadAsStringAsync(ct))).RootElement, "message"));
            return false;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Finance match-status write-back failed for invoice {Inv} (best-effort).", supplierInvoiceId);
            return false;
        }
    }

    /// <summary>Guards the error-body parse: anything that is not a JSON object (empty body, HTML error
    /// page, bare array) becomes an empty object so the message probe simply finds nothing.</summary>
    private static string SafeJson(string raw)
        => string.IsNullOrWhiteSpace(raw) || raw.TrimStart()[0] != '{' ? "{}" : raw;

    private static string? Str(JsonElement el, string name)
        => el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;

    private static decimal Dec(JsonElement el, string name)
        => el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Number ? p.GetDecimal() : 0m;
}
