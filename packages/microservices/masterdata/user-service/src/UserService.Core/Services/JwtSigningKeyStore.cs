using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace UserService.Core.Services;

/// <summary>
/// Holds the RSA keypair(s) user-service signs JWTs with (#215 — only user-service holds a private
/// key; every other service, and the gateway, verify via JWKS instead of a shared secret). Config is
/// a JSON array so rotation is adding a new entry rather than a redeploy of all 16 services: the
/// entry marked "active" signs new tokens, but ALL entries' public keys are published via
/// <see cref="BuildJwks"/>, so a token signed by a just-retired key still validates everywhere until
/// that entry is actually removed from the list — a config change, not an outage.
/// </summary>
public sealed class JwtSigningKeyStore
{
    private readonly List<(string Kid, RSA Rsa)> _keys = new();
    private readonly string _activeKid;

    public JwtSigningKeyStore(IConfiguration configuration)
    {
        var json = Environment.GetEnvironmentVariable("JWT_SIGNING_KEYS")
                   ?? configuration["JWT:SigningKeys"]
                   ?? throw new InvalidOperationException("JWT:SigningKeys is not configured.");

        var entries = JsonSerializer.Deserialize<List<KeyEntry>>(json)
                      ?? throw new InvalidOperationException("JWT:SigningKeys could not be parsed.");
        if (entries.Count == 0)
            throw new InvalidOperationException("JWT:SigningKeys must contain at least one key.");

        string? active = null;
        foreach (var e in entries)
        {
            var rsa = RSA.Create();
            rsa.ImportFromPem(e.PrivateKeyPem);
            _keys.Add((e.Kid, rsa));
            if (e.Active) active = e.Kid;
        }
        _activeKid = active ?? entries[0].Kid;
    }

    /// <summary>The one key new tokens are signed with.</summary>
    public SigningCredentials ActiveSigningCredentials
    {
        get
        {
            var (kid, rsa) = _keys.First(k => k.Kid == _activeKid);
            var key = new RsaSecurityKey(rsa) { KeyId = kid };
            return new SigningCredentials(key, SecurityAlgorithms.RsaSha256);
        }
    }

    /// <summary>Every key (active + any still-valid retired ones) — used to validate tokens by `kid`.</summary>
    public IReadOnlyList<SecurityKey> AllPublicSigningKeys =>
        _keys.Select(k => (SecurityKey)new RsaSecurityKey(k.Rsa) { KeyId = k.Kid }).ToList();

    /// <summary>The public JWKS document served at /.well-known/jwks.json.</summary>
    public JwksDocument BuildJwks()
    {
        var doc = new JwksDocument();
        foreach (var (kid, rsa) in _keys)
        {
            var p = rsa.ExportParameters(false);
            doc.Keys.Add(new JwkEntry
            {
                Kty = "RSA",
                Use = "sig",
                Alg = "RS256",
                Kid = kid,
                N = Base64UrlEncoder.Encode(p.Modulus),
                E = Base64UrlEncoder.Encode(p.Exponent),
            });
        }
        return doc;
    }

    private sealed class KeyEntry
    {
        [JsonPropertyName("kid")] public string Kid { get; set; } = "";
        [JsonPropertyName("privateKeyPem")] public string PrivateKeyPem { get; set; } = "";
        [JsonPropertyName("active")] public bool Active { get; set; }
    }
}

public sealed class JwksDocument
{
    [JsonPropertyName("keys")] public List<JwkEntry> Keys { get; set; } = new();
}

public sealed class JwkEntry
{
    [JsonPropertyName("kty")] public string Kty { get; set; } = "";
    [JsonPropertyName("use")] public string Use { get; set; } = "";
    [JsonPropertyName("alg")] public string Alg { get; set; } = "";
    [JsonPropertyName("kid")] public string Kid { get; set; } = "";
    [JsonPropertyName("n")] public string N { get; set; } = "";
    [JsonPropertyName("e")] public string E { get; set; } = "";
}
