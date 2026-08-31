using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ProcurementService.Core.Interfaces.Services;

namespace ProcurementService.Infrastructure.Services;

/// <summary>
/// P6 (DEC-4/DEC-A) — REAL payment handoff seam. A clean 3-way match posts the validated voucher to
/// Finance's existing payables voucher endpoint; Finance materialises its own PaymentVoucher against the
/// supplier invoice and routes it through the payment-authority matrix. Procurement holds no voucher table.
/// Mints a per-schema service token (finance.write). Fail-closed — nothing is stamped unless Finance confirms.
/// </summary>
public class HttpFinanceVoucherGateway(
    IHttpClientFactory httpClientFactory,
    IConfiguration config,
    IHttpContextAccessor httpContextAccessor,
    ILogger<HttpFinanceVoucherGateway> logger) : IPaymentVoucherGateway
{
    private string? BaseUrl => config["FinanceService:BaseUrl"]?.TrimEnd('/');
    private bool Enabled => config.GetValue("Finance:Enabled", false) && !string.IsNullOrWhiteSpace(BaseUrl);

    public async Task<VoucherResult> RaiseAsync(string supplierInvoiceId, decimal? amount, string? bankAccountCode, string poNumber, CancellationToken ct = default)
    {
        if (!Enabled)
            return new VoucherResult(false, null, null, "Finance is not configured, so no payment voucher can be raised.");

        var schema = httpContextAccessor.HttpContext?.User.FindFirst("schema")?.Value;
        if (string.IsNullOrWhiteSpace(schema))
            return new VoucherResult(false, null, null, "No tenant context for the Finance voucher handoff.");

        try
        {
            var client = httpClientFactory.CreateClient("FinanceService");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer", await ServiceToken.MintAsync(config, httpClientFactory, schema, "system.admin", "finance.write"));
            client.DefaultRequestHeaders.Add("X-Tenant-Schema", schema);

            // Amount omitted → Finance defaults the voucher to the outstanding invoice balance.
            var body = new
            {
                supplierInvoiceId,
                amount,
                bankAccountCode,
            };
            var resp = await client.PostAsync($"{BaseUrl}/api/v1/finance/vouchers",
                new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"), ct);
            var raw = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
            {
                // Surface Finance's own reason (e.g. "Approve the supplier invoice before raising a voucher.")
                // rather than a bare status code — it is the only actionable part of the failure.
                var reason = ReadMessage(raw);
                logger.LogWarning("Finance voucher post returned {Status} for LPO {Po}: {Reason}", resp.StatusCode, poNumber, reason);
                return new VoucherResult(false, null, null,
                    reason is null
                        ? $"Finance rejected the payment voucher ({(int)resp.StatusCode})."
                        : $"Finance rejected the payment voucher: {reason}");
            }

            using var doc = JsonDocument.Parse(raw);
            string? id = null, no = null;
            if (doc.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object)
            {
                id = data.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
                no = data.TryGetProperty("voucherNo", out var noEl) ? noEl.GetString() : null;
            }
            if (string.IsNullOrEmpty(id) && string.IsNullOrEmpty(no))
                return new VoucherResult(false, null, null, "Finance accepted the request but returned no voucher reference.");

            logger.LogInformation("Payment voucher {No} raised in Finance for LPO {Po}.", no ?? id, poNumber);
            return new VoucherResult(true, id, no, $"Payment voucher {no ?? id} raised in Finance for {poNumber}.");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Finance voucher handoff failed for LPO {Po} (fail-closed).", poNumber);
            return new VoucherResult(false, null, null, "Could not reach Finance to raise the payment voucher.");
        }
    }

    public async Task<VoucherResult> RaiseAdHocAsync(string payee, decimal amountKes, string reference, CancellationToken ct = default)
    {
        if (!Enabled)
            return new VoucherResult(false, null, null, "Finance is not configured, so no payment voucher can be raised.");
        if (amountKes <= 0)
            return new VoucherResult(false, null, null, "The advance amount must be greater than zero.");

        var schema = httpContextAccessor.HttpContext?.User.FindFirst("schema")?.Value;
        if (string.IsNullOrWhiteSpace(schema))
            return new VoucherResult(false, null, null, "No tenant context for the Finance voucher handoff.");

        try
        {
            var client = httpClientFactory.CreateClient("FinanceService");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer", await ServiceToken.MintAsync(config, httpClientFactory, schema, "system.admin", "finance.write"));
            client.DefaultRequestHeaders.Add("X-Tenant-Schema", schema);

            // Ad-hoc: no supplier invoice exists yet (this is an advance), so Finance takes payee + amount.
            var body = new { payee, amount = amountKes, supplierInvoiceId = (string?)null, bankAccountCode = (string?)null };
            var resp = await client.PostAsync($"{BaseUrl}/api/v1/finance/vouchers",
                new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"), ct);

            var raw = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
            {
                var reason = ReadMessage(raw);
                logger.LogWarning("Finance ad-hoc voucher returned {Status} for {Ref}: {Reason}", resp.StatusCode, reference, reason);
                return new VoucherResult(false, null, null,
                    reason is null
                        ? $"Finance rejected the advance voucher ({(int)resp.StatusCode})."
                        : $"Finance rejected the advance voucher: {reason}");
            }

            using var doc = JsonDocument.Parse(raw);
            string? id = null, no = null;
            if (doc.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object)
            {
                id = data.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
                no = data.TryGetProperty("voucherNo", out var noEl) ? noEl.GetString() : null;
            }
            if (string.IsNullOrEmpty(id) && string.IsNullOrEmpty(no))
                return new VoucherResult(false, null, null, "Finance accepted the request but returned no voucher reference.");

            logger.LogInformation("Advance voucher {No} raised in Finance for {Ref}.", no ?? id, reference);
            return new VoucherResult(true, id, no, $"Advance payment voucher {no ?? id} raised in Finance for {reference}.");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Finance ad-hoc voucher failed for {Ref} (fail-closed).", reference);
            return new VoucherResult(false, null, null, "Could not reach Finance to raise the advance voucher.");
        }
    }

    /// <summary>Pulls the <c>message</c> out of a Finance ApiResponse error body, if there is one.</summary>
    private static string? ReadMessage(string raw)
    {
        try
        {
            using var doc = JsonDocument.Parse(raw);
            return doc.RootElement.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String
                ? m.GetString() : null;
        }
        catch { return null; }
    }
}
