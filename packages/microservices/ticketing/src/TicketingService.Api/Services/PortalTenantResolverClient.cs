using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using TicketingService.Core.Interfaces.Services;

namespace TicketingService.Api.Services;

// Same shape as TenantEmailSettingsClient: cached with a per-slug gate so a burst of anonymous
// portal requests for the same company right after the cache entry expires doesn't each
// independently call out to user-service.
public class PortalTenantResolverClient(
    IHttpClientFactory httpClientFactory,
    IConfiguration config,
    IMemoryCache cache,
    ILogger<PortalTenantResolverClient> logger) : IPortalTenantResolverClient
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Gates = new();

    // Mirrors TenantSlug.IsValidFormat on the user-service side — reject obviously-malformed input
    // before it ever reaches a cache key or an outbound request.
    private static readonly System.Text.RegularExpressions.Regex SlugFormat =
        new(@"^[a-z][a-z0-9]*(-[a-z0-9]+)*$", System.Text.RegularExpressions.RegexOptions.Compiled);

    public async Task<string?> ResolveSchemaBySlugAsync(string? slug, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(slug)) return null;
        slug = slug.Trim().ToLowerInvariant();
        if (slug.Length is < 2 or > 50 || !SlugFormat.IsMatch(slug)) return null;

        var cacheKey = $"portal-tenant-schema:{slug}";
        if (cache.TryGetValue(cacheKey, out string? cached)) return cached;

        var gate = Gates.GetOrAdd(slug, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            if (cache.TryGetValue(cacheKey, out cached)) return cached;

            var resolved = await FetchAsync(slug, ct);
            // Cache misses too (short TTL) so a mistyped/retired slug doesn't hammer user-service.
            cache.Set(cacheKey, resolved, TimeSpan.FromSeconds(resolved is null ? 15 : 60));
            return resolved;
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<string?> FetchAsync(string slug, CancellationToken ct)
    {
        var baseUrl = config["UserService:BaseUrl"]?.TrimEnd('/');
        var serviceKey = config["InternalServices:ServiceKey"];
        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(serviceKey)) return null;

        try
        {
            using var client = httpClientFactory.CreateClient("UserService");
            client.DefaultRequestHeaders.Add("X-Internal-Key", serviceKey);

            var response = await client.GetAsync($"{baseUrl}/internal/tenants/by-slug/{Uri.EscapeDataString(slug)}", ct);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync(ct);
            var dto = JsonSerializer.Deserialize<TenantBySlugResponse>(json, JsonOpts);
            return dto is { IsActive: true } && !string.IsNullOrWhiteSpace(dto.SchemaName) ? dto.SchemaName : null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to resolve portal slug '{Slug}' to a tenant schema", slug);
            return null;
        }
    }

    private sealed record TenantBySlugResponse(string Id, string Name, string Slug, string SchemaName, bool IsActive);
}
