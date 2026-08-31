using UserService.Core.DTOs.Users;

namespace UserService.Core.Interfaces.Services;

/// <summary>
/// Authenticates a user against a specific tenant schema (schema-per-tenant), rather than the
/// shared public control plane. Used by the subdomain-resolved login path in Phase 3.
/// </summary>
public interface ITenantAuthenticator
{
    /// <summary>
    /// Verifies credentials against the given tenant schema's Users table and returns a populated
    /// <see cref="UserReadDto"/> (with SchemaName/TenantId/roles) on success, or null on failure.
    /// </summary>
    Task<UserReadDto?> AuthenticateAsync(
        string schema, string tenantId, string tenantName,
        string email, string password, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads a user (with roles + permissions) from the tenant schema by id, WITHOUT a password check.
    /// Used to complete a 2FA login against the same schema the credentials were verified against.
    /// </summary>
    Task<UserReadDto?> GetUserByIdAsync(
        string schema, string tenantId, string tenantName,
        string userId, CancellationToken cancellationToken = default);
}
