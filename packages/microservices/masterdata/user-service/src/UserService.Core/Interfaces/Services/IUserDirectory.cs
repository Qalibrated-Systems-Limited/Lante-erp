using UserService.Core.Entities;

namespace UserService.Core.Interfaces.Services;

/// <summary>
/// The control-plane user directory (public.Users), independent of the request's tenant search_path.
/// Single-login resolves users by email here; company-admin invites are written here so a newly
/// invited user can sign in at lante.africa without a per-tenant subdomain.
/// </summary>
public interface IUserDirectory
{
    /// <summary>Write an invited user (identity + role links + tenant link) to the control plane.</summary>
    Task CreateInvitedUserAsync(User user, IReadOnlyCollection<string>? roleIds, string? tenantId, string? branchId);

    /// <summary>
    /// Write a THIN directory pointer for a schema-per-tenant user: identity + tenant link only.
    /// Department/branch and any tenant-custom roles live in the tenant's own schema, so they are
    /// NOT written here (their IDs don't exist in public and would violate FKs). Only role IDs that
    /// also exist in public.Roles are linked, so single-login can still resolve baseline permissions.
    /// </summary>
    Task CreateDirectoryPointerAsync(User user, IReadOnlyCollection<string>? roleIds, string? tenantId);

    /// <summary>Find a directory user by hashed invite token (null if none).</summary>
    Task<User?> FindByInviteTokenHashAsync(string tokenHash);

    /// <summary>Set password, activate, and clear the invite token. Returns null if token invalid/expired.</summary>
    Task<User?> AcceptInviteAsync(string tokenHash, string passwordHash);

    /// <summary>
    /// Mirrors a just-updated password/first-login state into a schema-per-tenant user's tenant
    /// schema copy. Needed because password-changing endpoints (first-login update-password, admin
    /// reset) run against whichever schema the request's connection defaulted to — for a header-less
    /// call that's public — leaving the tenant schema's copy permanently stuck on stale credentials/
    /// first-login state otherwise. <paramref name="activate"/> should be true when this change also
    /// represents an activation event (first-login completion); false for a plain admin-triggered
    /// reset that shouldn't flip an otherwise-deactivated account back on. Best-effort/non-fatal,
    /// mirroring AcceptInviteAsync's tenant sync.
    /// </summary>
    /// <summary>
    /// Mirrors a user's auth-relevant fields (Email, Password, IsActive, IsFirstLogin,
    /// TwoFactorEnabled, InviteTokenHash, InviteTokenExpiresAt) into BOTH the public directory pointer
    /// and the tenant-schema copy, regardless of which one the caller's own write just landed on.
    /// Every request runs with an ambient schema (public, or a tenant schema — whichever the request's
    /// tenant header/interceptor picked), and any endpoint that mutates these fields only actually
    /// updates that one copy. For a schema-per-tenant user the other copy is a separate row that goes
    /// stale unless explicitly synced — and since login resolution sometimes reads the tenant schema
    /// (subdomain login) and sometimes reads public (subdomain-less/pre-auth), a stale copy on either
    /// side silently breaks login for that path. Call this with the just-updated User entity after any
    /// userRepository update that touches these fields, regardless of which side was ambient — the
    /// side that was ambient is simply re-written with its own already-current values (a harmless
    /// no-op), and the other side is brought back in sync. Best-effort/non-fatal on both sides.
    /// </summary>
    Task SyncUserAuthStateAsync(User user);
}
