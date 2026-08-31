namespace UserService.Core.Interfaces.Services;

/// <summary>
/// Keeps a schema-per-tenant user's baseline role assignment mirrored into the public control-plane
/// directory (public.UserRoles), for system roles that also exist in public.Roles (same Id across every
/// schema). Role assignment for a migrated tenant user is normally written only into that tenant's
/// schema (the request's search_path), leaving the public.Users/UserRoles directory record — the one a
/// header-less login (e.g. a mobile client that doesn't send X-Tenant-Subdomain) authenticates against —
/// stuck with whatever role it had at signup. Department/branch are NOT mirrored here: those are
/// tenant-specific foreign keys with no equivalent row in public, so copying them by value isn't safe.
/// </summary>
public interface IPublicRoleDirectorySync
{
    Task SyncRoleAssignedAsync(string userId, string roleId, CancellationToken cancellationToken = default);
    Task SyncRoleRemovedAsync(string userId, string roleId, CancellationToken cancellationToken = default);
}
