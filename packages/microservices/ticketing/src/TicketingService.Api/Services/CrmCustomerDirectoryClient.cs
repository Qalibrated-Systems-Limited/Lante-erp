using System.Text.Json;
using Microsoft.AspNetCore.Http;
using TicketingService.Core.Integrations;

namespace TicketingService.Api.Services;

/// <summary>
/// D8-1 — REAL CRM (Module 6) customer-master read. Reads the CRM internal customer endpoints so the
/// ticket-create picker can source from the org-wide CUSTOMER register. Service-to-service auth via the
/// shared <c>X-Internal-Key</c>; tenant carried on <c>X-Tenant-Schema</c> forwarded from the ambient
/// request. Enabled via <c>Integrations:Crm:Enabled</c> + <c>CrmService:BaseUrl</c>; returns empty/null
/// when disabled or unreachable so the local list stays the safe fallback.
/// </summary>
public class CrmCustomerDirectoryClient(
    IHttpClientFactory httpClientFactory,
    IConfiguration config,
    IHttpContextAccessor httpContextAccessor,
    ILogger<CrmCustomerDirectoryClient> logger) : ICrmCustomerDirectory
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private string? BaseUrl => config["CrmService:BaseUrl"]?.TrimEnd('/');

    public bool IsEnabled =>
        config.GetSection("Integrations:Crm")["Enabled"]?.Equals("true", StringComparison.OrdinalIgnoreCase) == true
        && !string.IsNullOrWhiteSpace(BaseUrl);

    public Task<IReadOnlyList<CrmCustomerRef>> SearchAsync(string? query, CancellationToken ct = default)
        => SearchInternalAsync(query, null, ct);

    private async Task<IReadOnlyList<CrmCustomerRef>> SearchInternalAsync(string? query, string? schema, CancellationToken ct)
    {
        if (!IsEnabled) return Array.Empty<CrmCustomerRef>();
        try
        {
            var q = string.IsNullOrWhiteSpace(query) ? "" : $"?search={Uri.EscapeDataString(query)}";
            using var doc = await GetAsync($"{BaseUrl}/internal/crm/customers{q}", ct, schema);
            if (doc is null || !doc.RootElement.TryGetProperty("data", out var arr) || arr.ValueKind != JsonValueKind.Array)
                return Array.Empty<CrmCustomerRef>();
            return arr.EnumerateArray().Select(Map).Where(c => c is not null).Cast<CrmCustomerRef>().ToList();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "CRM customer search failed for '{Query}' (falling back to local)", query);
            return Array.Empty<CrmCustomerRef>();
        }
    }

    public async Task<string?> ResolveIdByEmailAsync(string email, string? schema = null, CancellationToken ct = default)
    {
        if (!IsEnabled || string.IsNullOrWhiteSpace(email)) return null;
        try
        {
            // CRM's search is a substring match across name/email/reference, so it will surface the
            // exact address — but it can surface neighbours too. Narrow to rows whose email matches
            // exactly, and only anchor when precisely one survives.
            var candidates = await SearchInternalAsync(email, schema, ct);
            var exact = candidates
                .Where(c => string.Equals(c.Email, email, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (exact.Count != 1) return null;

            logger.LogInformation("Anchored CRM customer {CrmId} for {Email}.", exact[0].Id, email);
            return exact[0].Id;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "CRM customer resolve by email failed for '{Email}' (leaving unanchored)", email);
            return null;
        }
    }

    public async Task<CrmCustomerRef?> GetByIdAsync(string crmCustomerId, CancellationToken ct = default)
    {
        if (!IsEnabled || string.IsNullOrWhiteSpace(crmCustomerId)) return null;
        try
        {
            using var doc = await GetAsync($"{BaseUrl}/internal/crm/customers/{Uri.EscapeDataString(crmCustomerId)}", ct);
            if (doc is null || !doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
                return null;
            return Map(data);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "CRM customer lookup failed for {Id}", crmCustomerId);
            return null;
        }
    }

    private async Task<JsonDocument?> GetAsync(string url, CancellationToken ct, string? schema = null)
    {
        var client = httpClientFactory.CreateClient("CrmService");
        var serviceKey = config["InternalServices:ServiceKey"];
        if (!string.IsNullOrWhiteSpace(serviceKey))
            client.DefaultRequestHeaders.Add("X-Internal-Key", serviceKey);
        // Background passes supply the schema explicitly; request-scoped calls inherit it from the
        // ambient request. Sending none would let CRM answer from the wrong tenant's data.
        schema ??= httpContextAccessor.HttpContext?.Request.Headers["X-Tenant-Schema"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(schema))
            client.DefaultRequestHeaders.Add("X-Tenant-Schema", schema);

        var resp = await client.GetAsync(url, ct);
        if (!resp.IsSuccessStatusCode)
        {
            logger.LogWarning("CRM customer read {Url} returned {Status}", url, resp.StatusCode);
            return null;
        }
        return JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
    }

    private static CrmCustomerRef? Map(JsonElement c)
    {
        var id = c.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
        var name = c.TryGetProperty("name", out var nEl) ? nEl.GetString() : null;
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name)) return null;
        return new CrmCustomerRef(
            id, name,
            c.TryGetProperty("email", out var e) ? e.GetString() : null,
            c.TryGetProperty("phone", out var p) ? p.GetString() : null,
            c.TryGetProperty("clientReference", out var r) ? r.GetString() : null);
    }
}
