using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using TicketingService.Core.Interfaces.Services;

namespace TicketingService.Api.Services;

// Fetches the current tenant's custom SMTP settings from user-service, cached briefly so every
// outbound email doesn't hit user-service. Uses a per-tenant gate around the cache-miss path —
// see LanteGateway's tenant-suspension cache for why a plain IMemoryCache.GetOrCreateAsync isn't
// enough (it doesn't dedupe concurrent misses, so a burst of emails right after the TTL expires
// would otherwise each independently call out).
public class TenantEmailSettingsClient(
    IHttpClientFactory httpClientFactory,
    IConfiguration config,
    IHttpContextAccessor httpContextAccessor,
    IMemoryCache cache,
    ILogger<TenantEmailSettingsClient> logger) : ITenantEmailSettingsClient
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Gates = new();

    public async Task<TenantSmtpSettings?> GetSettingsAsync()
    {
        var schema = CurrentTenantSchema();
        if (string.IsNullOrWhiteSpace(schema)) return null; // no tenant context — use the platform default

        var cacheKey = $"tenant-smtp:{schema}";
        if (cache.TryGetValue(cacheKey, out TenantSmtpSettings? cached)) return cached;

        var gate = Gates.GetOrAdd(schema, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync();
        try
        {
            if (cache.TryGetValue(cacheKey, out cached)) return cached;

            var fetched = await FetchAsync(schema);
            cache.Set(cacheKey, fetched, TimeSpan.FromSeconds(60));
            return fetched;
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<TenantSmtpSettings?> FetchAsync(string schema)
    {
        var baseUrl = config["UserService:BaseUrl"]?.TrimEnd('/');
        var serviceKey = config["InternalServices:ServiceKey"];
        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(serviceKey)) return null;

        try
        {
            using var client = httpClientFactory.CreateClient("UserService");
            client.DefaultRequestHeaders.Add("X-Internal-Key", serviceKey);
            client.DefaultRequestHeaders.Add("X-Tenant-Schema", schema);

            var response = await client.GetAsync($"{baseUrl}/internal/tenants/email-settings");
            if (response.StatusCode == System.Net.HttpStatusCode.NoContent) return null;
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("UserService returned {Status} fetching email settings for schema {Schema}",
                    response.StatusCode, schema);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            var dto = JsonSerializer.Deserialize<InternalEmailSettingsResponse>(json, JsonOpts);
            if (dto is null) return null;

            return new TenantSmtpSettings(dto.SmtpHost, dto.SmtpPort, dto.SmtpUsername, dto.SmtpPassword, dto.FromEmail, dto.FromName);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to fetch tenant email settings for schema {Schema}; using platform default", schema);
            return null;
        }
    }

    // JWT claim wins for authenticated requests. For anonymous portal requests there is no claim —
    // PortalTenantMiddleware resolves the slug server-side and stamps the validated schema onto
    // HttpContext.Items, which is safe to trust here (the client can't set Items, only headers).
    // Deliberately does NOT fall back to the raw X-Tenant-Schema request header the way the DB
    // connection interceptor's internal-key-gated fallback does — this class has no way to check
    // for a valid X-Internal-Key, so trusting that header here would let anyone hitting the public
    // portal pick an arbitrary tenant's SMTP settings just by setting a header.
    private string? CurrentTenantSchema()
    {
        var ctx = httpContextAccessor.HttpContext;
        if (ctx == null) return null;
        return ctx.User.FindFirst("schema")?.Value
            ?? ctx.Items["ResolvedTenantSchema"] as string;
    }

    private sealed record InternalEmailSettingsResponse(
        string SmtpHost, int SmtpPort, string SmtpUsername, string SmtpPassword, string FromEmail, string FromName);
}
