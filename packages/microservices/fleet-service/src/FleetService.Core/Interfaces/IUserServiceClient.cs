namespace FleetService.Core.Interfaces;

public record UserContactDto(string Id, string Name, string? Email, string? MobileNumber);

public interface IUserServiceClient
{
    // Service-to-service lookup (user-service's internal/users/{id}) — schema must be passed
    // explicitly since this call has no JWT/schema claim of its own to resolve it from. Returns
    // null (and logs) on any failure, so a user-service outage never blocks the caller's own flow.
    Task<UserContactDto?> GetUserContactAsync(string tenantSchema, string userId);

    // Fans out to everyone holding a permission (e.g. fleet.write) rather than one known user.
    // Returns an empty list (and logs) on any failure — never blocks the caller's own flow.
    Task<IEnumerable<UserContactDto>> GetUsersByPermissionAsync(string tenantSchema, string permission);

    // Narrower than the above — matches a specific role by name (e.g. "Fleet Manager") instead of
    // everyone who happens to hold a permission that role's job also requires (e.g. Fleet Staff).
    Task<IEnumerable<UserContactDto>> GetUsersByRoleNameAsync(string tenantSchema, string roleName);
}
