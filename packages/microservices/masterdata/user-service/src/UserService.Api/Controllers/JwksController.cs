using Microsoft.AspNetCore.Mvc;
using UserService.Core.Services;

namespace UserService.Api.Controllers;

/// <summary>
/// Publishes user-service's public signing key(s) so every other service — and the gateway — can
/// verify a JWT without ever holding the private key (#215). Deliberately unauthenticated: a JWKS
/// document is public key material by definition, the same as any OIDC provider's discovery endpoint.
/// </summary>
[ApiController]
[Route(".well-known")]
public class JwksController(JwtSigningKeyStore keyStore) : ControllerBase
{
    [HttpGet("jwks.json")]
    public IActionResult Get() => Ok(keyStore.BuildJwks());
}
