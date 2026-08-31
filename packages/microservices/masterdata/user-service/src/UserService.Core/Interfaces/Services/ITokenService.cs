using UserService.Core.DTOs.Users;
using UserService.Core.Entities;

namespace UserService.Core.Interfaces.Services;

public interface ITokenService
{
    Task<PersonalAccessToken> GenerateTokenForAuthenticatedUserAsync(UserReadDto user);

    /// <summary>Short-lived, carries no roles or permissions, and authorises only a first-password set.
    /// Minted by login once the temporary password has been verified. See #263.</summary>
    Task<PersonalAccessToken> GeneratePasswordChangeTokenAsync(UserReadDto user);
    /// <summary>Builds a signed JWT (with tenant/schema claims) WITHOUT persisting a control-plane
    /// PersonalAccessToken. Used for schema-per-tenant logins whose users don't live in public.</summary>
    (string token, string jti) CreateJwt(UserReadDto user);
    Task<bool> ValidateTokenAsync(string token);
    Task<Guid?> GetUserIdFromTokenAsync(string token);
    Task<bool> RevokeTokenAsync(string token);
    Task<bool> DeleteAllTokensForUserAsync(Guid userId);
}
