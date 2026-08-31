using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;

namespace ProcurementService.Infrastructure.Services;

/// <summary>Requests a short-lived, real-key-signed service token from user-service's internal
/// system-token endpoint (#215) — calling a sibling service (Finance, Operations) with the tenant
/// <c>schema</c> claim so the target's interceptor scopes search_path correctly, plus one or more
/// <c>permission</c> claims to satisfy that service's authorization policies. Previously hand-rolled
/// an HS256 token locally with the shared secret; now every service (except user-service itself)
/// lacks the private key, so this is a network call, cached briefly to avoid one per outbound
/// request.</summary>
internal static class ServiceToken
{
    private sealed record CacheEntry(string Token, DateTime ExpiresAt);
    private static readonly ConcurrentDictionary<string, CacheEntry> Cache = new();

    public static async Task<string> MintAsync(IConfiguration config, IHttpClientFactory httpClientFactory,
        string schema, params string[] permissions)
    {
        var perms = permissions is { Length: > 0 } ? permissions : new[] { "system.admin" };
        var cacheKey = schema + "|" + string.Join(",", perms);

        if (Cache.TryGetValue(cacheKey, out var cached) && cached.ExpiresAt > DateTime.UtcNow.AddSeconds(20))
            return cached.Token;

        var baseUrl = (config["UserService:BaseUrl"] ?? config["UserService__BaseUrl"]
            ?? "http://lante-user-service:8080").TrimEnd('/');
        var internalKey = config["InternalServices:ServiceKey"]
            ?? throw new InvalidOperationException("InternalServices:ServiceKey not configured for service calls.");

        var client = httpClientFactory.CreateClient("SystemToken");
        client.DefaultRequestHeaders.Add("X-Internal-Key", internalKey);

        var resp = await client.PostAsJsonAsync($"{baseUrl}/internal/system-token",
            new { schema, permissions = perms });
        resp.EnsureSuccessStatusCode();

        var body = await resp.Content.ReadFromJsonAsync<SystemTokenResponse>()
            ?? throw new InvalidOperationException("system-token response was empty.");

        Cache[cacheKey] = new CacheEntry(body.Token, body.ExpiresAt);
        return body.Token;
    }

    private sealed record SystemTokenResponse(string Token, DateTime ExpiresAt);
}
