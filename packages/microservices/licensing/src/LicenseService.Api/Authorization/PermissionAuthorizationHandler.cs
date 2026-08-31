using Microsoft.AspNetCore.Authorization;

namespace LicenseService.Api.Authorization;

public class PermissionAuthorizationHandler(ILogger<PermissionAuthorizationHandler> logger)
    : AuthorizationHandler<PermissionRequirement>
{
    private static readonly Dictionary<string, string[]> _hierarchy = new(StringComparer.OrdinalIgnoreCase)
    {
        ["tickets.read.own"]   = ["tickets.read.own", "tickets.write", "tickets.assign", "tickets.resolve", "tickets.delete", "tickets.read.dept", "tickets.read.all", "system.admin"],
        ["tickets.read.dept"]  = ["tickets.read.dept", "tickets.read.all", "system.admin"],
        ["tickets.read.all"]   = ["tickets.read.all", "system.admin"],
        ["projects.read.own"]  = ["projects.read.own", "projects.write", "projects.delete", "projects.approve", "projects.read.dept", "projects.read.all", "system.admin"],
        ["projects.read.dept"] = ["projects.read.dept", "projects.write", "projects.delete", "projects.approve", "projects.read.all", "system.admin"],
        ["projects.read.all"]  = ["projects.read.all", "projects.approve", "system.admin"],
        // Kept in agreement with fleet-service's own copy, the actual enforcer of this
        // permission — this service never checks it locally, but a stale copy here is exactly
        // the class of drift #204 is about.
        ["fleet.read"]         = ["fleet.read", "fleet.write", "fleet.delete", "fleet.expenses", "fleet.dispatch.request", "system.admin"],
        ["fleet.write"]        = ["fleet.write", "fleet.delete", "system.admin"],
        ["fleet.delete"]       = ["fleet.delete", "system.admin"],
        ["fleet.expenses"]     = ["fleet.expenses", "fleet.write", "fleet.delete", "system.admin"],
        ["technician.read"]    = ["technician.read", "technician.write", "technician.delete", "technician.approve", "system.admin"],
        ["technician.write"]   = ["technician.write", "technician.delete", "technician.approve", "system.admin"],
        ["technician.delete"]  = ["technician.delete", "system.admin"],
        ["technician.approve"] = ["technician.approve", "system.admin"],
        // Deliberately NOT chained to "system.admin" or to each other: "system.admin" means "full
        // admin within your OWN tenant" everywhere else on the platform, but this service manages
        // every CUSTOMER's software license (Token/CustomerId/AppId) — a genuine platform-wide
        // resource, not tenant data. A tenant Admin's system.admin (which every tenant Admin role
        // carries) must not grant this. Only platform.licensing.manage — held solely by
        // role-platform-admin, never seeded into any tenant role — may. See HandleRequirementAsync
        // below for the matching system.admin bypass exclusion.
        ["licensing.read"]     = ["licensing.read", "licensing.write", "licensing.delete", "platform.licensing.manage"],
        ["licensing.write"]    = ["licensing.write", "licensing.delete", "platform.licensing.manage"],
        ["licensing.delete"]   = ["licensing.delete", "platform.licensing.manage"],
    };

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (context.User?.Identity?.IsAuthenticated != true)
        {
            logger.LogWarning("Unauthenticated access attempt for permission: {Permission}", requirement.Permission);
            context.Fail();
            return Task.CompletedTask;
        }

        var userPermissions = context.User.Claims
            .Where(c => c.Type == "permission")
            .Select(c => c.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // "licensing.*" is exempt from the system.admin shortcut — see the _hierarchy comment
        // above. Every other resource in this service keeps the usual bypass.
        var isLicensingResource = requirement.Permission.StartsWith("licensing.", StringComparison.OrdinalIgnoreCase);

        if (!isLicensingResource && userPermissions.Contains("system.admin"))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        var accepted = _hierarchy.TryGetValue(requirement.Permission, out var group)
            ? group
            : [requirement.Permission];

        if (accepted.Any(p => userPermissions.Contains(p)))
            context.Succeed(requirement);
        else
        {
            logger.LogWarning("Permission denied. Required: {Permission}", requirement.Permission);
            context.Fail();
        }

        return Task.CompletedTask;
    }
}
