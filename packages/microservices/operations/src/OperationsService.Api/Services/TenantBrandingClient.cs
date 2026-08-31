using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace OperationsService.Api.Services;

public interface ITenantBrandingClient
{
    Task<CertificatePdfService.TenantBranding?> GetBrandingAsync();
}

// Fetches the current tenant's own company identity from user-service, cached briefly so every
// certificate render doesn't hit user-service. Mirrors TicketingService.Api.Services.TenantEmailSettingsClient's
// per-tenant gate around the cache-miss path — see that class for why a plain
// IMemoryCache.GetOrCreateAsync isn't enough on its own.
public class TenantBrandingClient(
    IHttpClientFactory httpClientFactory,
    IConfiguration config,
    IHttpContextAccessor httpContextAccessor,
    IMemoryCache cache,
    ILogger<TenantBrandingClient> logger) : ITenantBrandingClient
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Gates = new();

    public async Task<CertificatePdfService.TenantBranding?> GetBrandingAsync()
    {
        var schema = CurrentTenantSchema();
        if (string.IsNullOrWhiteSpace(schema)) return null; // no tenant context — use the default identity

        var cacheKey = $"tenant-branding:{schema}";
        if (cache.TryGetValue(cacheKey, out CertificatePdfService.TenantBranding? cached)) return cached;

        var gate = Gates.GetOrAdd(schema, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync();
        try
        {
            if (cache.TryGetValue(cacheKey, out cached)) return cached;

            var fetched = await FetchAsync(schema);
            cache.Set(cacheKey, fetched, TimeSpan.FromMinutes(5));
            return fetched;
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<CertificatePdfService.TenantBranding?> FetchAsync(string schema)
    {
        var baseUrl = config["UserService:BaseUrl"]?.TrimEnd('/');
        var serviceKey = config["InternalServices:ServiceKey"];
        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(serviceKey)) return null;

        try
        {
            using var client = httpClientFactory.CreateClient("UserService");
            client.DefaultRequestHeaders.Add("X-Internal-Key", serviceKey);
            client.DefaultRequestHeaders.Add("X-Tenant-Schema", schema);

            var response = await client.GetAsync($"{baseUrl}/internal/tenants/branding");
            if (response.StatusCode == HttpStatusCode.NoContent) return null;
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("UserService returned {Status} fetching branding for schema {Schema}",
                    response.StatusCode, schema);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            var dto = JsonSerializer.Deserialize<InternalBrandingResponse>(json, JsonOpts);
            if (dto is null || string.IsNullOrWhiteSpace(dto.LegalName)) return null;

            var contact = string.Join(" · ", new[] { dto.Phone, dto.Email }.Where(s => !string.IsNullOrWhiteSpace(s)));
            return new CertificatePdfService.TenantBranding(dto.LegalName, dto.Address ?? string.Empty, contact, dto.DocCodePrefix);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to fetch tenant branding for schema {Schema}; using default identity", schema);
            return null;
        }
    }

    // JWT claim wins for authenticated requests; PortalTenantMiddleware's resolved schema (for
    // anonymous callers) is trusted the same way the DB connection interceptor trusts it.
    private string? CurrentTenantSchema()
    {
        var ctx = httpContextAccessor.HttpContext;
        if (ctx == null) return null;
        return ctx.User.FindFirst("schema")?.Value
            ?? ctx.Items["ResolvedTenantSchema"] as string;
    }

    private sealed record InternalBrandingResponse(string? LegalName, string? DisplayName, string? Address, string? Phone, string? Email, string? DocCodePrefix);
}
