using System.Collections.Concurrent;
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;

namespace ReportingService.Infrastructure.Services;

/// <summary>
/// Requests a short-lived JWT for the scheduler's own upstream calls from user-service's internal
/// system-token endpoint (#215). ReportsController's *ReportService classes forward the CALLING
/// USER's bearer token (see BaseServiceClient) — there is no such user for an unattended scheduled
/// run, so this asks user-service — the only service that still holds a private signing key — to
/// mint one carrying "system.admin" (which every service's PermissionAuthorizationHandler treats as
/// an unconditional pass) plus the tenant "schema" claim so TenantDbConnectionInterceptor scopes
/// upstream queries to the right tenant. Previously signed this locally with the shared secret.
/// </summary>
public class SystemTokenIssuer(IConfiguration config, IHttpClientFactory httpClientFactory)
{
    private sealed record CacheEntry(string Token, DateTime ExpiresAt);
    private static readonly ConcurrentDictionary<string, CacheEntry> Cache = new();

    public async Task<string> IssueForAsync(string tenantSchema)
    {
        if (Cache.TryGetValue(tenantSchema, out var cached) && cached.ExpiresAt > DateTime.UtcNow.AddSeconds(20))
            return cached.Token;

        var baseUrl = (config["UserService:BaseUrl"] ?? config["UserService__BaseUrl"]
            ?? "http://lante-user-service:8080").TrimEnd('/');
        var internalKey = config["InternalServices:ServiceKey"]
            ?? throw new InvalidOperationException("InternalServices:ServiceKey not configured for service calls.");

        var client = httpClientFactory.CreateClient("SystemToken");
        client.DefaultRequestHeaders.Add("X-Internal-Key", internalKey);

        var resp = await client.PostAsJsonAsync($"{baseUrl}/internal/system-token",
            new { schema = tenantSchema, permissions = new[] { "system.admin" } });
        resp.EnsureSuccessStatusCode();

        var body = await resp.Content.ReadFromJsonAsync<SystemTokenResponse>()
            ?? throw new InvalidOperationException("system-token response was empty.");

        Cache[tenantSchema] = new CacheEntry(body.Token, body.ExpiresAt);
        return body.Token;
    }

    private sealed record SystemTokenResponse(string Token, DateTime ExpiresAt);
}
