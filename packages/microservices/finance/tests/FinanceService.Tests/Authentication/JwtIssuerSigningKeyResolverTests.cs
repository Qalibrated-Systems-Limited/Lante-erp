using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using FinanceService.Api.Authentication;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace FinanceService.Tests.Authentication;

public class JwtIssuerSigningKeyResolverTests
{
    private sealed class StaticJwksHandler(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) });
    }

    private static (string jwksJson, RSA rsa, string kid) MakeJwks()
    {
        var rsa = RSA.Create(2048);
        var kid = "resolver-test-kid";
        var p = rsa.ExportParameters(false);
        var jwks = new
        {
            keys = new[]
            {
                new
                {
                    kty = "RSA", use = "sig", alg = "RS256", kid,
                    n = Base64UrlEncoder.Encode(p.Modulus),
                    e = Base64UrlEncoder.Encode(p.Exponent),
                }
            }
        };
        return (JsonSerializer.Serialize(jwks), rsa, kid);
    }

    [Fact]
    public void ResolveSigningKeys_ReturnsTheKeyMatchingKid()
    {
        var (json, _, kid) = MakeJwks();
        var http = new HttpClient(new StaticJwksHandler(json));
        var resolver = new JwtIssuerSigningKeyResolver(http, "http://fake/.well-known/jwks.json");

        var keys = resolver.ResolveSigningKeys("token", null!, kid, new TokenValidationParameters()).ToList();

        Assert.Single(keys);
        Assert.Equal(kid, keys[0].KeyId);
    }

    [Fact]
    public void ResolveSigningKeys_ReturnsNoKeys_ForAnUnknownKid()
    {
        var (json, _, _) = MakeJwks();
        var http = new HttpClient(new StaticJwksHandler(json));
        var resolver = new JwtIssuerSigningKeyResolver(http, "http://fake/.well-known/jwks.json");

        var keys = resolver.ResolveSigningKeys("token", null!, "some-other-kid", new TokenValidationParameters()).ToList();

        Assert.Empty(keys);
    }

    [Fact]
    public void ResolveSigningKeys_ATokenSignedByThePublishedKey_ValidatesSuccessfully()
    {
        var (json, rsa, kid) = MakeJwks();
        var http = new HttpClient(new StaticJwksHandler(json));
        var resolver = new JwtIssuerSigningKeyResolver(http, "http://fake/.well-known/jwks.json");

        var privateKey = new RsaSecurityKey(rsa) { KeyId = kid };
        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var token = handler.CreateEncodedJwt(new SecurityTokenDescriptor
        {
            Issuer = "LanteUserService",
            Audience = "LanteUserService",
            Expires = DateTime.UtcNow.AddMinutes(5),
            SigningCredentials = new SigningCredentials(privateKey, SecurityAlgorithms.RsaSha256),
        });

        var principal = handler.ValidateToken(token, new TokenValidationParameters
        {
            ValidIssuer = "LanteUserService",
            ValidAudience = "LanteUserService",
            IssuerSigningKeyResolver = (t, st, k, p) => resolver.ResolveSigningKeys(t, st, k!, p),
        }, out _);

        Assert.NotNull(principal);
    }
}
