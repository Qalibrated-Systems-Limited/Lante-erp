namespace UserService.Core.Constants;

/// <summary>
/// Role names that the code makes security decisions on.
///
/// <para>Authorization here keys on a role's <b>display name</b>, not its id — <c>user.Roles</c> carries
/// names, and <c>[Authorize(Roles = "...")]</c> matches the role claim, which is a name. That makes the
/// name a security-critical value rather than a label, and it must not be edited casually.</para>
///
/// <para><see cref="Role.IsSystem"/> is what protects it, and these constants exist so the several places
/// that compare against the literal move together. Two of those comparisons <b>fail open</b> if the name
/// stops matching — see the callers — so a silent drift is not a cosmetic problem.</para>
/// </summary>
public static class WellKnownRoles
{
    /// <summary>
    /// The platform operator. Sits outside every tenant, administers all of them, and is deliberately
    /// barred from the ordinary tenant login portal.
    /// </summary>
    public const string PlatformAdmin = "Platform Admin";

    /// <summary>Seeded role ids that must never be renamed, deleted or deactivated.</summary>
    public static readonly string[] LockedRoleIds = ["role-platform-admin"];
}
