using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Moq;
using UserService.Core.DTOs.Users;
using UserService.Core.Entities;
using UserService.Core.Interfaces.Repositories;
using UserService.Core.Services;
using Xunit;

namespace UserService.Tests.Services;

public class TokenServiceTests
{
    private readonly Mock<ITokenRepository> _tokenRepo = new();

    // #215 — signing moved from a shared HS256 secret to an RSA keypair only user-service holds.
    // Generated fresh per test run rather than a hardcoded PEM, so nothing key-shaped lives in source.
    private static string BuildTestSigningKeysJson(string kid = "test-kid-1")
    {
        using var rsa = RSA.Create(2048);
        var pem = rsa.ExportRSAPrivateKeyPem();
        return JsonSerializer.Serialize(new[] { new { kid, privateKeyPem = pem, active = true } });
    }

    private TokenService CreateSut()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JWT:SigningKeys"] = BuildTestSigningKeysJson(),
                ["JwtSettings:Issuer"] = "LanteUserService",
                ["JwtSettings:Audience"] = "LanteUserService",
                ["JWT__TokenExpiryMinutes"] = "480"
            })
            .Build();

        return new TokenService(config, _tokenRepo.Object, new JwtSigningKeyStore(config));
    }

    private UserReadDto MakeUserDto(string? id = null) => new()
    {
        Id = id ?? Guid.NewGuid().ToString(),
        Email = "user@lante.com",
        FirstName = "Test",
        LastName = "User",
        Roles = ["Employee"]
    };

    [Fact]
    public async Task GenerateTokenForAuthenticatedUserAsync_ReturnsToken()
    {
        var user = MakeUserDto();
        _tokenRepo.Setup(r => r.CreateAsync(It.IsAny<PersonalAccessToken>()))
            .ReturnsAsync((PersonalAccessToken t) => t);

        var result = await CreateSut().GenerateTokenForAuthenticatedUserAsync(user);

        Assert.NotNull(result);
        Assert.NotEmpty(result.Token);
        Assert.False(result.IsRevoked);
        Assert.NotEmpty(result.Jti);
    }

    [Fact]
    public async Task GenerateTokenForAuthenticatedUserAsync_StoresTokenInRepo()
    {
        var user = MakeUserDto();
        _tokenRepo.Setup(r => r.CreateAsync(It.IsAny<PersonalAccessToken>()))
            .ReturnsAsync((PersonalAccessToken t) => t);

        await CreateSut().GenerateTokenForAuthenticatedUserAsync(user);

        _tokenRepo.Verify(r => r.CreateAsync(It.Is<PersonalAccessToken>(t =>
            t.UserId == user.Id &&
            !t.IsRevoked &&
            !string.IsNullOrEmpty(t.Token)
        )), Times.Once);
    }

    [Fact]
    public async Task ValidateTokenAsync_ReturnsFalse_ForNullOrEmpty()
    {
        var sut = CreateSut();

        Assert.False(await sut.ValidateTokenAsync(null!));
        Assert.False(await sut.ValidateTokenAsync(""));
        Assert.False(await sut.ValidateTokenAsync("   "));
    }

    [Fact]
    public async Task ValidateTokenAsync_ReturnsFalse_ForMalformedToken()
    {
        var result = await CreateSut().ValidateTokenAsync("not.a.valid.jwt.at.all");
        Assert.False(result);
    }

    [Fact]
    public async Task ValidateTokenAsync_ReturnsFalse_ForRevokedToken()
    {
        var user = MakeUserDto();
        PersonalAccessToken? stored = null;

        _tokenRepo.Setup(r => r.CreateAsync(It.IsAny<PersonalAccessToken>()))
            .ReturnsAsync((PersonalAccessToken t) => { stored = t; return t; });

        var sut = CreateSut();
        var pat = await sut.GenerateTokenForAuthenticatedUserAsync(user);

        stored!.IsRevoked = true;
        _tokenRepo.Setup(r => r.GetTokenByJtiAsync(stored.Jti)).ReturnsAsync(stored);

        var result = await sut.ValidateTokenAsync(pat.Token);
        Assert.False(result);
    }

    [Fact]
    public async Task ValidateTokenAsync_ReturnsTrue_ForValidToken()
    {
        var user = MakeUserDto();
        PersonalAccessToken? stored = null;

        _tokenRepo.Setup(r => r.CreateAsync(It.IsAny<PersonalAccessToken>()))
            .ReturnsAsync((PersonalAccessToken t) => { stored = t; return t; });

        var sut = CreateSut();
        var pat = await sut.GenerateTokenForAuthenticatedUserAsync(user);

        _tokenRepo.Setup(r => r.GetTokenByJtiAsync(stored!.Jti)).ReturnsAsync(stored);

        var result = await sut.ValidateTokenAsync(pat.Token);
        Assert.True(result);
    }

    [Fact]
    public async Task RevokeTokenAsync_SetsIsRevoked_AndUpdatesRepo()
    {
        var user = MakeUserDto();
        PersonalAccessToken? stored = null;

        _tokenRepo.Setup(r => r.CreateAsync(It.IsAny<PersonalAccessToken>()))
            .ReturnsAsync((PersonalAccessToken t) => { stored = t; return t; });

        var sut = CreateSut();
        var pat = await sut.GenerateTokenForAuthenticatedUserAsync(user);

        _tokenRepo.Setup(r => r.GetTokenByJtiAsync(stored!.Jti)).ReturnsAsync(stored);
        _tokenRepo.Setup(r => r.UpdateAsync(It.IsAny<PersonalAccessToken>()))
            .ReturnsAsync((PersonalAccessToken t) => t);

        var result = await sut.RevokeTokenAsync(pat.Token);

        Assert.True(result);
        _tokenRepo.Verify(r => r.UpdateAsync(It.Is<PersonalAccessToken>(t => t.IsRevoked)), Times.Once);
    }

    [Fact]
    public async Task GetUserIdFromTokenAsync_ReturnsUserId_ForValidToken()
    {
        var userId = Guid.NewGuid().ToString().ToString();
        var user = MakeUserDto(userId);

        _tokenRepo.Setup(r => r.CreateAsync(It.IsAny<PersonalAccessToken>()))
            .ReturnsAsync((PersonalAccessToken t) => t);

        var sut = CreateSut();
        var pat = await sut.GenerateTokenForAuthenticatedUserAsync(user);

        var result = await sut.GetUserIdFromTokenAsync(pat.Token);

        Assert.NotNull(result);
        Assert.Equal(Guid.Parse(userId), result);
    }

    [Fact]
    public async Task DeleteAllTokensForUserAsync_DelegatesToRepository()
    {
        var userId = Guid.NewGuid();
        _tokenRepo.Setup(r => r.DeleteAllTokensForUserAsync(userId)).ReturnsAsync(true);

        var result = await CreateSut().DeleteAllTokensForUserAsync(userId);

        Assert.True(result);
        _tokenRepo.Verify(r => r.DeleteAllTokensForUserAsync(userId), Times.Once);
    }

    // --- #215: RSA signing / JWKS ---

    [Fact]
    public async Task CreateJwt_IsSignedWithRs256_AndCarriesTheActiveKid()
    {
        _tokenRepo.Setup(r => r.CreateAsync(It.IsAny<PersonalAccessToken>()))
            .ReturnsAsync((PersonalAccessToken t) => t);

        var pat = await CreateSut().GenerateTokenForAuthenticatedUserAsync(MakeUserDto());

        var jwt = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().ReadJwtToken(pat.Token);
        Assert.Equal("RS256", jwt.Header.Alg);
        Assert.Equal("test-kid-1", jwt.Header.Kid);
    }

    [Fact]
    public void BuildJwks_PublishesAPublicKeyThatIndependentlyValidatesAnIssuedToken()
    {
        var signingKeysJson = BuildTestSigningKeysJson("jwks-round-trip-kid");
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["JWT:SigningKeys"] = signingKeysJson })
            .Build();
        var store = new JwtSigningKeyStore(config);

        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var token = handler.CreateEncodedJwt(new Microsoft.IdentityModel.Tokens.SecurityTokenDescriptor
        {
            Issuer = "LanteUserService",
            Audience = "LanteUserService",
            Expires = DateTime.UtcNow.AddMinutes(5),
            SigningCredentials = store.ActiveSigningCredentials,
        });

        var jwks = store.BuildJwks();
        var jwk = Assert.Single(jwks.Keys);
        Assert.Equal("jwks-round-trip-kid", jwk.Kid);

        var rsa = RSA.Create(new RSAParameters
        {
            Modulus = Microsoft.IdentityModel.Tokens.Base64UrlEncoder.DecodeBytes(jwk.N),
            Exponent = Microsoft.IdentityModel.Tokens.Base64UrlEncoder.DecodeBytes(jwk.E),
        });
        var publicKey = new Microsoft.IdentityModel.Tokens.RsaSecurityKey(rsa) { KeyId = jwk.Kid };

        var principal = handler.ValidateToken(token, new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidIssuer = "LanteUserService",
            ValidAudience = "LanteUserService",
            IssuerSigningKey = publicKey,
        }, out _);

        Assert.NotNull(principal);
    }

    [Fact]
    public void BuildJwks_TokenDoesNotValidate_AgainstADifferentKeysPublicKey()
    {
        var store = new JwtSigningKeyStore(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["JWT:SigningKeys"] = BuildTestSigningKeysJson("kid-a") })
            .Build());
        var otherStore = new JwtSigningKeyStore(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["JWT:SigningKeys"] = BuildTestSigningKeysJson("kid-b") })
            .Build());

        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var token = handler.CreateEncodedJwt(new Microsoft.IdentityModel.Tokens.SecurityTokenDescriptor
        {
            Issuer = "LanteUserService",
            Audience = "LanteUserService",
            Expires = DateTime.UtcNow.AddMinutes(5),
            SigningCredentials = store.ActiveSigningCredentials,
        });

        var wrongJwk = Assert.Single(otherStore.BuildJwks().Keys);
        var rsa = RSA.Create(new RSAParameters
        {
            Modulus = Microsoft.IdentityModel.Tokens.Base64UrlEncoder.DecodeBytes(wrongJwk.N),
            Exponent = Microsoft.IdentityModel.Tokens.Base64UrlEncoder.DecodeBytes(wrongJwk.E),
        });

        Assert.Throws<Microsoft.IdentityModel.Tokens.SecurityTokenSignatureKeyNotFoundException>(() =>
            handler.ValidateToken(token, new Microsoft.IdentityModel.Tokens.TokenValidationParameters
            {
                ValidIssuer = "LanteUserService",
                ValidAudience = "LanteUserService",
                IssuerSigningKey = new Microsoft.IdentityModel.Tokens.RsaSecurityKey(rsa) { KeyId = wrongJwk.Kid },
            }, out _));
    }
}
