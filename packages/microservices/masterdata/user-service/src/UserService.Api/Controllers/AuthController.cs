using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UserService.Core.Constants;
using UserService.Core.DTOs.Auth;
using UserService.Core.DTOs.Users;
using UserService.Core.Interfaces.Repositories;
using UserService.Core.Interfaces.Services;
using UserService.Core.Services;

namespace UserService.Api.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/auth")]
[Asp.Versioning.ApiVersion("1.0")]
public class AuthController(
    ITokenService tokenService,
    IUserService userService,
    ITwoFactorService twoFactorService,
    ITenantRepository tenantRepository,
    ITenantAuthenticator tenantAuthenticator,
    ILogger<AuthController> logger)
    : BaseController
{
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginDto loginDto)
    {
        if (!ModelState.IsValid) return BadRequestResult("Invalid request.");

        // Schema-per-tenant: if the request carries a tenant subdomain that resolves to a provisioned
        // schema, authenticate against that schema. Otherwise fall back to the public control plane
        // (platform, and tenants not yet migrated out of public).
        var subdomain = Request.Headers["X-Tenant-Subdomain"].FirstOrDefault()?.Trim().ToLowerInvariant();
        UserReadDto? user;
        var authenticatedViaTenantSchema = false;
        if (!string.IsNullOrWhiteSpace(subdomain))
        {
            var tenant = await tenantRepository.GetBySlugAsync(subdomain);
            if (tenant != null && !string.IsNullOrEmpty(tenant.SchemaName))
            {
                try
                {
                    user = await tenantAuthenticator.AuthenticateAsync(tenant.SchemaName, tenant.Id, tenant.Name, loginDto.Email, loginDto.Password);
                    authenticatedViaTenantSchema = true;
                }
                catch (Npgsql.PostgresException ex) when (ex.SqlState == "42P01") // undefined_table → schema not provisioned yet
                {
                    return BadRequestResult("This company is still being set up. Please try again shortly or contact your administrator.");
                }
            }
            else
                user = await userService.ValidateUserCredentials(loginDto.Email, loginDto.Password);
        }
        else
        {
            user = await userService.ValidateUserCredentials(loginDto.Email, loginDto.Password);
        }

        if (user == null) return UnauthorizedResult("Invalid email or password.");

        // Callers that don't send X-Tenant-Subdomain (e.g. the mobile app) authenticated against the
        // public control-plane directory above. For a schema-per-tenant user that record is only a thin
        // pointer — role/department/branch/company-admin are frequently stale there, since day-to-day
        // edits made while an admin is scoped into the tenant only land in the tenant schema (see
        // UserServiceTenantConnectionInterceptor). Re-resolve from the tenant schema so every login path
        // returns the same, current data.
        if (!authenticatedViaTenantSchema)
            user = await ResolveAuthoritativeUserAsync(user);

        // FAIL-OPEN if the name ever stops matching: the platform operator would silently become able to
        // sign in through the ordinary tenant portal. The constant keeps every comparison together, and
        // Role.IsSystem stops the name being edited in the first place.
        if (user.Roles.Contains(WellKnownRoles.PlatformAdmin))
            return UnauthorizedResult("Invalid email or password.");

        if (!string.IsNullOrEmpty(user.TenantId))
        {
            var tenant = await tenantRepository.GetByIdAsync(user.TenantId);
            if (tenant != null && !tenant.IsActive)
                return UnauthorizedResult("Your company's account has been suspended. Please contact your account manager to restore access.");
        }

        if (user.IsFirstLogin)
        {
            // The temporary password has just been verified, so this is the one moment we can vouch for
            // the caller. That proof is handed back as a short-lived, password-change-only token; the
            // update-password endpoint accepts nothing else (#263).
            var changeToken = await tokenService.GeneratePasswordChangeTokenAsync(user);
            return OkResult(new { message = "First login detected", userId = user.Id,
                                  passwordChangeToken = changeToken.Token,
                                  redirectUrl = $"/api/v1/auth/update-password/{user.Id}" });
        }

        if (!user.TwoFactorEnabled)
        {
            string directTokenString;
            if (!string.IsNullOrEmpty(user.SchemaName))
            {
                // Schema-per-tenant user: issue the JWT only (their session/PAT + active flag live in
                // the tenant schema, not the control plane). Signature-based request auth is unaffected.
                directTokenString = tokenService.CreateJwt(user).token;
            }
            else
            {
                var directToken = await tokenService.GenerateTokenForAuthenticatedUserAsync(user);
                await userService.UpdateUserActiveStatusAsync(user.Id, true);
                directTokenString = directToken.Token;
            }
            return OkResult(new LoginResponseDto
            {
                Token         = directTokenString,
                Id            = user.Id,
                Email         = user.Email,
                FirstName     = user.FirstName,
                LastName      = user.LastName,
                DepartmentId  = user.DepartmentId,
                DepartmentIds = user.DepartmentIds,
                UserRoles     = user.Roles,
                Permissions   = user.Permissions,
                TenantId      = user.TenantId,
                TenantName    = user.TenantName,
                BranchId      = user.BranchId,
                BranchName    = user.BranchName,
                HqBranchId    = user.HqBranchId,
                IsCompanyAdmin = user.IsCompanyAdmin
            }, "Successfully logged in.");
        }

        var sessionId = await twoFactorService.CreateTwoFactorSessionAsync(user.Id, user.SchemaName);
        var codeResult = await twoFactorService.GenerateAndSendCodeAsync(user.Id, user.Email);

        if (!codeResult.Success)
            return BadRequestResult(codeResult.Message);

        var response = new TwoFactorResponseDto
        {
            Requires2FA = true,
            SessionId = sessionId,
            Message = "Verification code sent to your email. Please enter the code to complete login.",
            Email = MaskEmail(user.Email)
        };

        return OkResult(response);
    }

    [HttpPost("verify-2fa")]
    [AllowAnonymous]
    public async Task<IActionResult> VerifyTwoFactor([FromBody] TwoFactorRequestDto request)
    {
        if (!ModelState.IsValid) return BadRequestResult("Invalid request.");

        var userId = await twoFactorService.GetUserIdFromSessionAsync(request.SessionId);
        if (string.IsNullOrEmpty(userId))
            return BadRequestResult("Invalid or expired session.");

        var verifyResult = await twoFactorService.VerifyCodeAsync(request.SessionId, request.Code);
        if (!verifyResult.Success)
            return BadRequestResult(verifyResult.Message);

        // Single-login: credentials were verified against the public control-plane directory, so the
        // session's userId is the public id — reload from public here too (mirrors the non-2FA login
        // path). The user's SchemaName (resolved via their tenant) still rides in the JWT `schema`
        // claim so data routes to the tenant schema after login.
        var user = await userService.GetByIdAsync(userId);
        if (user == null) return BadRequestResult("User not found.");
        user = await ResolveAuthoritativeUserAsync(user);
        string tokenString;
        if (!string.IsNullOrEmpty(user.SchemaName))
        {
            // Tenant user: JWT only (their session/PAT + active flag are not tracked in the control plane).
            tokenString = tokenService.CreateJwt(user).token;
        }
        else
        {
            var token = await tokenService.GenerateTokenForAuthenticatedUserAsync(user);
            await userService.UpdateUserActiveStatusAsync(userId, true);
            tokenString = token.Token;
        }

        var response = new LoginResponseDto
        {
            Token         = tokenString,
            Id            = user.Id,
            Email         = user.Email,
            FirstName     = user.FirstName,
            LastName      = user.LastName,
            DepartmentId  = user.DepartmentId,
            DepartmentIds = user.DepartmentIds,
            UserRoles     = user.Roles,
            Permissions   = user.Permissions,
            TenantId      = user.TenantId,
            TenantName    = user.TenantName,
            BranchId      = user.BranchId,
            BranchName    = user.BranchName,
            HqBranchId    = user.HqBranchId,
            IsCompanyAdmin = user.IsCompanyAdmin
        };

        return OkResult(response, "Successfully logged in.");
    }

    // ── Invites (single-login) ─────────────────────────────────────────────────
    // A company admin creates a user → the identity lands in the control-plane directory (public.Users)
    // with a hashed, expiring, single-use token, and an invite link is emailed. These two endpoints back
    // the /accept-invite page: validate the token, then let the user set their own password. Both run
    // pre-auth (public search_path), so the directory lookup resolves correctly.
    [HttpGet("invite/{token}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetInvite(string token)
    {
        var info = await userService.GetInviteAsync(token);
        if (info == null)
            return BadRequestResult("This invite link is invalid or has expired. Please ask your administrator to resend it.");
        return OkResult(info);
    }

    [HttpPost("accept-invite")]
    [AllowAnonymous]
    public async Task<IActionResult> AcceptInvite([FromBody] AcceptInviteDto dto)
    {
        if (!ModelState.IsValid) return BadRequestResult("Invalid request.");
        var (ok, error) = await userService.AcceptInviteAsync(dto.Token, dto.NewPassword, dto.ConfirmPassword);
        if (!ok) return BadRequestResult(error ?? "Could not accept the invite.");
        return OkResult(new { message = "Your password has been set. You can now sign in." });
    }

    [HttpPut("update-password/{userId}")]
    [AllowAnonymous]
    public async Task<IActionResult> UpdatePassword(string userId, [FromBody] UpdatePasswordDto dto)
    {
        if (!ModelState.IsValid) return BadRequestResult("Invalid request.");

        // First-login password change runs pre-auth (no JWT yet, so no `schema` claim). If the request
        // carries a tenant subdomain, resolve it to the tenant schema and set X-Tenant-Schema so the
        // connection interceptor binds search_path to tenant_<slug>; otherwise this user (who lives in
        // the tenant schema) would be looked up in public and 404.
        var subdomain = Request.Headers["X-Tenant-Subdomain"].FirstOrDefault()?.Trim().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(subdomain))
        {
            var tenant = await tenantRepository.GetBySlugAsync(subdomain);
            if (tenant != null && !string.IsNullOrEmpty(tenant.SchemaName))
                Request.Headers["X-Tenant-Schema"] = tenant.SchemaName;
        }

        // Establish WHO is calling before touching the password. The endpoint stays AllowAnonymous
        // because a first-login caller has no session yet, but "anonymous" must not mean "unauthenticated":
        // the bearer token is required and must belong to this user.
        //
        //   - a password-change-scoped token (issued by login, above) authorises setting a FIRST password
        //     without knowing the temporary one;
        //   - an ordinary session token authorises a self-service change, which still verifies the current
        //     password downstream.
        //
        // Anything else — no token, another user's token, an expired or revoked one — is refused. Before
        // this, the user id in the URL was the only input, and IsFirstLogin defaults to true (#263).
        var (authorised, identityProven, failure) = await AuthorisePasswordChangeAsync(userId);
        if (!authorised) return UnauthorizedResult(failure ?? "This password-change link is invalid or has expired.");

        var user = await userService.UpdatePassword(userId, dto, identityProven);
        if (user != null) user = await ResolveAuthoritativeUserAsync(user);

        // The scoped token is spent. Leaving it live would leave a second, weaker credential for the
        // account lying around for the rest of its lifetime.
        if (identityProven) await tokenService.RevokeTokenAsync(ExtractTokenFromHeader() ?? string.Empty);
        var token = await tokenService.GenerateTokenForAuthenticatedUserAsync(user!);
        await userService.UpdateUserActiveStatusAsync(userId, true);

        var response = new LoginResponseDto
        {
            Token         = token.Token,
            Id            = user!.Id,
            Email         = user.Email,
            FirstName     = user.FirstName,
            LastName      = user.LastName,
            DepartmentId  = user.DepartmentId,
            DepartmentIds = user.DepartmentIds,
            UserRoles     = user.Roles,
            Permissions   = user.Permissions,
            TenantId      = user.TenantId,
            TenantName    = user.TenantName,
            BranchId      = user.BranchId,
            BranchName    = user.BranchName,
            HqBranchId    = user.HqBranchId,
            IsCompanyAdmin = user.IsCompanyAdmin
        };

        return OkResult(response, "Password updated successfully.");
    }

    // Separate platform login — only allows role-platform-admin through
    [HttpPost("platform/login")]
    [AllowAnonymous]
    public async Task<IActionResult> PlatformLogin([FromBody] LoginDto loginDto)
    {
        if (!ModelState.IsValid) return BadRequestResult("Invalid request.");

        var user = await userService.ValidateUserCredentials(loginDto.Email, loginDto.Password);
        if (user == null) return UnauthorizedResult("Invalid email or password.");

        if (!user.Roles.Contains(WellKnownRoles.PlatformAdmin))
            return ForbiddenResult("This login portal is for platform administrators only.");

        if (user.IsFirstLogin)
        {
            var platformChangeToken = await tokenService.GeneratePasswordChangeTokenAsync(user);
            return OkResult(new { message = "First login detected", userId = user.Id,
                                  passwordChangeToken = platformChangeToken.Token,
                                  redirectUrl = $"/api/v1/auth/update-password/{user.Id}" });
        }

        if (!user.TwoFactorEnabled)
        {
            var directToken = await tokenService.GenerateTokenForAuthenticatedUserAsync(user);
            await userService.UpdateUserActiveStatusAsync(user.Id, true);
            return OkResult(new LoginResponseDto
            {
                Token      = directToken.Token,
                Id         = user.Id,
                Email      = user.Email,
                FirstName  = user.FirstName,
                LastName   = user.LastName,
                UserRoles  = user.Roles,
                Permissions = user.Permissions
            }, "Welcome to Lante Platform.");
        }

        var sessionId = await twoFactorService.CreateTwoFactorSessionAsync(user.Id, user.SchemaName);
        var codeResult = await twoFactorService.GenerateAndSendCodeAsync(user.Id, user.Email);

        if (!codeResult.Success) return BadRequestResult(codeResult.Message);

        return OkResult(new TwoFactorResponseDto
        {
            Requires2FA = true,
            SessionId   = sessionId,
            Message     = "Verification code sent to your email.",
            Email       = MaskEmail(user.Email)
        });
    }

    [HttpPost("switch-branch")]
    [Authorize]
    public async Task<IActionResult> SwitchBranch([FromBody] SwitchBranchDto dto)
    {
        var token = ExtractTokenFromHeader();
        if (string.IsNullOrEmpty(token)) return BadRequestResult("No token provided.");

        var userId = await tokenService.GetUserIdFromTokenAsync(token);
        if (userId == null) return UnauthorizedResult("Invalid token.");

        var user = await userService.GetByIdAsync(userId.Value.ToString());
        if (user == null) return NotFoundResult("User not found.");

        // Verify the user has access to the requested branch
        var userTenant = await tenantRepository.GetDefaultTenantForUserAsync(user.Id);
        if (userTenant == null) return ForbiddenResult("User has no tenant assignment.");

        if (!string.IsNullOrEmpty(dto.BranchId))
        {
            var branch = await tenantRepository.GetBranchByIdAsync(dto.BranchId);
            if (branch == null || branch.TenantId != userTenant.TenantId)
                return ForbiddenResult("Branch not found in your tenant.");
        }

        // Override the branch in the user DTO and issue a new token
        user.BranchId      = dto.BranchId;
        user.IsCompanyAdmin = string.IsNullOrEmpty(dto.BranchId);

        await tokenService.RevokeTokenAsync(token);
        var newToken = await tokenService.GenerateTokenForAuthenticatedUserAsync(user);

        return OkResult(new { token = newToken.Token, branchId = dto.BranchId });
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        var token = ExtractTokenFromHeader();
        if (string.IsNullOrEmpty(token)) return BadRequestResult("No token provided.");

        var userId = await tokenService.GetUserIdFromTokenAsync(token);
        if (userId == null)
        {
            try
            {
                var handler = new JwtSecurityTokenHandler();
                var jwt = handler.ReadJwtToken(token);
                var claim = jwt.Claims.FirstOrDefault(c =>
                    c.Type == ClaimTypes.NameIdentifier || c.Type == JwtRegisteredClaimNames.Sub);
                if (claim != null && Guid.TryParse(claim.Value, out var parsed))
                    userId = parsed;
                else
                    return BadRequestResult("Invalid token format.");
            }
            catch
            {
                return BadRequestResult("Invalid token.");
            }
        }

        await userService.UpdateUserActiveStatusAsync(userId.Value.ToString(), false);
        await tokenService.DeleteAllTokensForUserAsync(userId.Value);

        logger.LogInformation("User {UserId} logged out", userId.Value);
        return OkResult(new { message = "Successfully logged out." });
    }

    /// <summary>
    /// Re-reads a schema-per-tenant user's role/department/branch/permissions/company-admin flag from
    /// their tenant schema, discarding whatever the public-directory DTO carried for those fields. Only
    /// TenantId/TenantName/SchemaName need to already be resolved on <paramref name="user"/> (done by
    /// UserService.PopulateTenantContextAsync); this fetches the rest from the authoritative source.
    /// Returns <paramref name="user"/> unchanged if it isn't a migrated tenant user, or if the tenant
    /// schema unexpectedly has no matching record (logged as a warning — should not normally happen).
    /// </summary>
    private async Task<UserReadDto> ResolveAuthoritativeUserAsync(UserReadDto user)
    {
        if (string.IsNullOrEmpty(user.SchemaName) || string.IsNullOrEmpty(user.TenantId))
            return user;

        var tenantUser = await tenantAuthenticator.GetUserByIdAsync(
            user.SchemaName, user.TenantId, user.TenantName ?? string.Empty, user.Id);

        if (tenantUser == null)
        {
            logger.LogWarning(
                "User {UserId} resolved to tenant schema {Schema} via the public directory but has no matching tenant-schema record.",
                user.Id, user.SchemaName);
            return user;
        }

        return tenantUser;
    }

    /// <summary>
    /// Decides whether this caller may change <paramref name="userId"/>'s password, and whether their
    /// identity is proven well enough to skip the current-password check.
    ///
    /// <para>Returns <c>identityProven: true</c> ONLY for a password-change-scoped token belonging to this
    /// same user. An ordinary session token authorises the request but proves nothing about knowing the
    /// current password, so that check still runs downstream.</para>
    /// </summary>
    private async Task<(bool authorised, bool identityProven, string? failure)> AuthorisePasswordChangeAsync(string userId)
    {
        var raw = ExtractTokenFromHeader();
        if (string.IsNullOrWhiteSpace(raw))
            return (false, false, "You need to sign in before changing a password.");

        if (!await tokenService.ValidateTokenAsync(raw))
            return (false, false, "This password-change link is invalid or has expired. Please sign in again.");

        var tokenUserId = await tokenService.GetUserIdFromTokenAsync(raw);
        // The token must belong to the account being changed. Without this, any signed-in user could set
        // any other user's password simply by putting their id in the URL — a smaller hole than the
        // anonymous one, but the same hole.
        if (tokenUserId is null || !string.Equals(tokenUserId.Value.ToString(), userId, StringComparison.OrdinalIgnoreCase))
            return (false, false, "You can only change your own password.");

        var scoped = ReadScopeClaim(raw) == TokenService.PasswordChangeScope;
        return (true, scoped, null);
    }

    /// <summary>Reads the scope claim without re-validating — the caller has already validated the token.</summary>
    private static string? ReadScopeClaim(string token)
    {
        try
        {
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
            return jwt.Claims.FirstOrDefault(c => c.Type == TokenService.PasswordChangeScopeClaim)?.Value;
        }
        catch { return null; }
    }

    private string? ExtractTokenFromHeader()
    {
        var authHeader = Request.Headers["Authorization"].FirstOrDefault();
        if (string.IsNullOrEmpty(authHeader)) return null;
        return authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? authHeader.Substring("Bearer ".Length).Trim()
            : authHeader.Trim();
    }

    private static string MaskEmail(string email)
    {
        if (string.IsNullOrEmpty(email) || !email.Contains('@')) return email;
        var parts = email.Split('@');
        var local = parts[0];
        var domain = parts[1];
        return local.Length <= 2
            ? $"{local[0]}***@{domain}"
            : $"{local[0]}***{local[^1]}@{domain}";
    }
   
    [HttpPost("google-login")]
    [AllowAnonymous]
    public async Task<IActionResult> GoogleLogin([FromBody] GoogleAuthDto dto)
    {
        if (!ModelState.IsValid) return BadRequestResult("Invalid request.");

        var user = await userService.GoogleLoginAsync(dto.IdToken);
        if (user == null) return UnauthorizedResult("Invalid Google token, or no account exists for this email. Contact your administrator for an invite.");
        user = await ResolveAuthoritativeUserAsync(user);

        if (!string.IsNullOrEmpty(user.TenantId))
        {
            var tenant = await tenantRepository.GetByIdAsync(user.TenantId);
            if (tenant != null && !tenant.IsActive)
                return UnauthorizedResult("Your company's account has been suspended. Please contact your account manager to restore access.");
        }

        var token = await tokenService.GenerateTokenForAuthenticatedUserAsync(user);
        await userService.UpdateUserActiveStatusAsync(user.Id, true);

        return OkResult(new LoginResponseDto
        {
            Token          = token.Token,
            Id             = user.Id,
            Email          = user.Email,
            FirstName      = user.FirstName,
            LastName       = user.LastName,
            DepartmentId   = user.DepartmentId,
            DepartmentIds  = user.DepartmentIds,
            UserRoles      = user.Roles,
            Permissions    = user.Permissions,
            TenantId       = user.TenantId,
            TenantName     = user.TenantName,
            BranchId       = user.BranchId,
            BranchName     = user.BranchName,
            HqBranchId     = user.HqBranchId,
            IsCompanyAdmin = user.IsCompanyAdmin
        }, "Successfully logged in.");
    }
}
