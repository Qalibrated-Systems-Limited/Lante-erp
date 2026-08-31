using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using UserService.Api.Authorization;
using UserService.Core.Services;

namespace UserService.Api.Controllers;

public sealed record SystemTokenRequest(string Schema, string[]? Permissions);

/// <summary>
/// Mints short-lived, real-key-signed service-to-service tokens (#215). Before this, five services
/// (crm, hr, operations, procurement, reporting) each hand-rolled their own HS256 JWT using the
/// shared secret — the exact issuance-sprawl this whole change removes. Now they call here instead,
/// authenticated the same way every other internal endpoint already is.
/// </summary>
[ApiController]
[Route("internal/system-token")]
[ServiceKeyAuthorize]
public class InternalSystemTokenController(JwtSigningKeyStore keyStore, IConfiguration configuration) : ControllerBase
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(2);

    [HttpPost]
    public IActionResult Mint([FromBody] SystemTokenRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Schema))
            return BadRequest(new { message = "schema is required." });

        var issuer = configuration["JwtSettings:Issuer"] ?? configuration["JWT__Issuer"] ?? "LanteUserService";
        var audience = configuration["JwtSettings:Audience"] ?? configuration["JWT__Audience"] ?? "LanteUserService";
        var perms = request.Permissions is { Length: > 0 } ? request.Permissions : new[] { "system.admin" };
        var now = DateTime.UtcNow;

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "system-service-gateway"),
            new(ClaimTypes.Name, "System Service Gateway"),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("schema", request.Schema),
        };
        claims.AddRange(perms.Select(p => new Claim("permission", p)));

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: now.AddSeconds(-10),
            expires: now.Add(Lifetime),
            signingCredentials: keyStore.ActiveSigningCredentials);

        return Ok(new { token = new JwtSecurityTokenHandler().WriteToken(token), expiresAt = now.Add(Lifetime) });
    }
}
