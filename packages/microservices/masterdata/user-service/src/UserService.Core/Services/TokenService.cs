using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using UserService.Core.DTOs.Users;
using UserService.Core.Entities;
using UserService.Core.Interfaces.Repositories;
using UserService.Core.Interfaces.Services;

namespace UserService.Core.Services;

public class TokenService : ITokenService
{
    private readonly JwtSigningKeyStore _keyStore;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly TimeSpan _expiration;
    private readonly ITokenRepository _tokenRepository;

    public TokenService(IConfiguration configuration, ITokenRepository tokenRepository, JwtSigningKeyStore keyStore)
    {
        _tokenRepository = tokenRepository;
        _keyStore = keyStore;

        _issuer = Environment.GetEnvironmentVariable("JWT_ISSUER")
                 ?? configuration["JWT__Issuer"]
                 ?? configuration["JwtSettings:Issuer"]
                 ?? "LanteUserService";

        _audience = Environment.GetEnvironmentVariable("JWT_AUDIENCE")
                   ?? configuration["JWT__Audience"]
                   ?? configuration["JwtSettings:Audience"]
                   ?? "LanteUserService";

        _expiration = TimeSpan.FromMinutes(
            configuration.GetValue<int>("JWT:TokenExpiryMinutes",
                configuration.GetValue<int>("JwtSettings:ExpiryInMinutes", 480)));
    }

    /// <summary>Long enough to type a password, short enough that a leaked link is worthless.</summary>
    public static readonly TimeSpan PasswordChangeTokenLifetime = TimeSpan.FromMinutes(15);

    public const string PasswordChangeScopeClaim = "scope";
    public const string PasswordChangeScope = "password-change";

    public async Task<PersonalAccessToken> GenerateTokenForAuthenticatedUserAsync(UserReadDto user)
    {
        var (tokenString, jti) = CreateJwt(user);
        var personalAccessToken = new PersonalAccessToken
        {
            Token = tokenString,
            UserId = user.Id,
            Jti = jti,
            TenantId = user.TenantId,
            BranchId = user.BranchId,
            IsRevoked = false,
            ExpiresAt = DateTime.UtcNow.Add(_expiration)
        };
        return await _tokenRepository.CreateAsync(personalAccessToken);
    }

    /// <summary>
    /// A short-lived token that authorises exactly one thing: setting this user's first password.
    ///
    /// <para>Minted by login AFTER the temporary password has been verified, so the caller has already
    /// proved who they are. Before this existed, <c>update-password</c> trusted the user id in the URL
    /// alone — and because <c>IsFirstLogin</c> defaults to true, any anonymous caller who knew an id could
    /// take over the account and be handed a full session (#263).</para>
    ///
    /// <para>Deliberately carries NO roles and NO permissions, so it cannot be used as a session even
    /// though it is a well-formed JWT, and it expires in minutes rather than hours. It is recorded as a
    /// PersonalAccessToken so it can be revoked the moment it is spent.</para>
    /// </summary>
    public async Task<PersonalAccessToken> GeneratePasswordChangeTokenAsync(UserReadDto user)
    {
        var jti = Guid.NewGuid().ToString();
        var expires = DateTime.UtcNow.Add(PasswordChangeTokenLifetime);

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(JwtRegisteredClaimNames.Sub, user.Id),
            new Claim(JwtRegisteredClaimNames.Jti, jti),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(PasswordChangeScopeClaim, PasswordChangeScope),
        };
        // The schema claim is kept because the connection interceptor needs it to find a tenant user;
        // without it the lookup lands in public and the account looks missing.
        if (!string.IsNullOrEmpty(user.SchemaName)) claims.Add(new Claim("schema", user.SchemaName));
        if (!string.IsNullOrEmpty(user.TenantId)) claims.Add(new Claim("tenant_id", user.TenantId));

        var jwt = new JwtSecurityToken(
            issuer: _issuer, audience: _audience, claims: claims, expires: expires,
            signingCredentials: _keyStore.ActiveSigningCredentials);

        return await _tokenRepository.CreateAsync(new PersonalAccessToken
        {
            Token = new JwtSecurityTokenHandler().WriteToken(jwt),
            UserId = user.Id,
            Jti = jti,
            TenantId = user.TenantId,
            BranchId = user.BranchId,
            IsRevoked = false,
            ExpiresAt = expires,
        });
    }

    public (string token, string jti) CreateJwt(UserReadDto user)
    {
        var jti = Guid.NewGuid().ToString();
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(JwtRegisteredClaimNames.Sub, user.Id),
            new Claim(JwtRegisteredClaimNames.Jti, jti),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, user.Email),
        };

        foreach (var role in user.Roles)
            claims.Add(new Claim(ClaimTypes.Role, role));

        foreach (var permission in user.Permissions)
            claims.Add(new Claim("permission", permission));

        if (!string.IsNullOrEmpty(user.DepartmentId))
            claims.Add(new Claim("department_id", user.DepartmentId));

        if (user.DepartmentIds.Count > 0)
            claims.Add(new Claim("department_ids", string.Join(",", user.DepartmentIds)));

        if (!string.IsNullOrEmpty(user.TenantId))
            claims.Add(new Claim("tenant_id", user.TenantId));

        // Schema-per-tenant: the schema this user's data lives in. Consumed by the connection
        // interceptor (SET search_path) and gateway (X-Tenant-Schema). Empty for control-plane/public.
        if (!string.IsNullOrEmpty(user.SchemaName))
            claims.Add(new Claim("schema", user.SchemaName));

        if (!string.IsNullOrEmpty(user.TenantName))
            claims.Add(new Claim("tenant_name", user.TenantName));

        if (!string.IsNullOrEmpty(user.BranchId))
            claims.Add(new Claim("branch_id", user.BranchId));

        if (!string.IsNullOrEmpty(user.BranchName))
            claims.Add(new Claim("branch_name", user.BranchName));

        if (!string.IsNullOrEmpty(user.HqBranchId))
            claims.Add(new Claim("hq_branch_id", user.HqBranchId));

        claims.Add(new Claim("is_company_admin", user.IsCompanyAdmin.ToString().ToLower()));

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            expires: DateTime.UtcNow.Add(_expiration),
            signingCredentials: _keyStore.ActiveSigningCredentials
        );

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
        return (tokenString, jti);
    }

    public async Task<bool> ValidateTokenAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return false;

        token = token.Replace("Bearer ", "", StringComparison.OrdinalIgnoreCase).Trim();
        if (token.Split('.').Length != 3) return false;

        var tokenHandler = new JwtSecurityTokenHandler();

        var validationResult = await tokenHandler.ValidateTokenAsync(token, new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = _issuer,
            ValidateAudience = true,
            ValidAudience = _audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            IssuerSigningKeyResolver = (_, __, kid, ___) =>
                kid is null ? _keyStore.AllPublicSigningKeys : _keyStore.AllPublicSigningKeys.Where(k => k.KeyId == kid)
        });

        if (!validationResult.IsValid) return false;

        var jwtToken = tokenHandler.ReadJwtToken(token);
        var jti = jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti)?.Value;
        if (string.IsNullOrWhiteSpace(jti)) return false;

        var dbToken = await _tokenRepository.GetTokenByJtiAsync(jti);
        return dbToken != null && !dbToken.IsRevoked;
    }

    public Task<Guid?> GetUserIdFromTokenAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return Task.FromResult<Guid?>(null);

        try
        {
            token = token.Replace("Bearer ", "", StringComparison.OrdinalIgnoreCase).Trim();
            var tokenHandler = new JwtSecurityTokenHandler();
            var jwtToken = tokenHandler.ReadJwtToken(token);
            var userIdClaim = jwtToken.Claims.FirstOrDefault(c =>
                c.Type == ClaimTypes.NameIdentifier || c.Type == JwtRegisteredClaimNames.Sub);

            if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var userId))
                return Task.FromResult<Guid?>(userId);
        }
        catch (Exception ex)
        {
            Log.Warning("Failed to parse token in GetUserIdFromTokenAsync: {Message}", ex.Message);
        }

        return Task.FromResult<Guid?>(null);
    }

    public async Task<bool> RevokeTokenAsync(string token)
    {
        token = token.Replace("Bearer ", "", StringComparison.OrdinalIgnoreCase).Trim();
        var tokenHandler = new JwtSecurityTokenHandler();
        var jwtToken = tokenHandler.ReadJwtToken(token);
        var jti = jwtToken.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti)?.Value;
        if (string.IsNullOrWhiteSpace(jti)) return false;

        var dbToken = await _tokenRepository.GetTokenByJtiAsync(jti);
        if (dbToken == null) return false;

        dbToken.IsRevoked = true;
        await _tokenRepository.UpdateAsync(dbToken);
        return true;
    }

    public async Task<bool> DeleteAllTokensForUserAsync(Guid userId)
    {
        return await _tokenRepository.DeleteAllTokensForUserAsync(userId);
    }
}
