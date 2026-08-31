namespace LicenseService.Api.Authentication;

using Microsoft.IdentityModel.Tokens;

/// <summary>
/// Resolves JWT signing keys from user-service's JWKS endpoint instead of a shared secret (#215).
/// Only user-service holds the private key; every other service (and the gateway) verifies via its
/// public JWKS document, matched by `kid`. Cached for 10 minutes so a key rotation (new `kid` added
/// to the JWKS) becomes visible without a redeploy.
/// </summary>
public sealed class JwtIssuerSigningKeyResolver
{
    private readonly HttpClient _http;
    private readonly string _jwksUrl;
    private readonly object _lock = new();
    private IList<SecurityKey> _keys = new List<SecurityKey>();
    private DateTime _fetchedAt = DateTime.MinValue;
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromMinutes(10);

    public JwtIssuerSigningKeyResolver(HttpClient http, string jwksUrl)
    {
        _http = http;
        _jwksUrl = jwksUrl;
    }

    public IEnumerable<SecurityKey> ResolveSigningKeys(string token, SecurityToken securityToken,
        string kid, TokenValidationParameters validationParameters)
    {
        EnsureFresh();
        lock (_lock)
            return (kid is null ? _keys : _keys.Where(k => k.KeyId == kid)).ToList();
    }

    private void EnsureFresh()
    {
        lock (_lock)
        {
            if (DateTime.UtcNow - _fetchedAt < CacheLifetime && _keys.Count > 0) return;
        }

        string json;
        try
        {
            json = _http.GetStringAsync(_jwksUrl).GetAwaiter().GetResult();
        }
        catch
        {
            // Keep serving the last-known-good key set if user-service is briefly unreachable —
            // in-flight tokens should still validate rather than every request failing closed.
            lock (_lock) { if (_keys.Count > 0) return; }
            throw;
        }

        var jwks = new JsonWebKeySet(json);
        var keys = jwks.GetSigningKeys().ToList();
        lock (_lock)
        {
            _keys = keys;
            _fetchedAt = DateTime.UtcNow;
        }
    }
}
