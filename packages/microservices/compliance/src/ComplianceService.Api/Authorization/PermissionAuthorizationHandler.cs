using Microsoft.AspNetCore.Authorization;

namespace ComplianceService.Api.Authorization;

public class PermissionAuthorizationHandler(ILogger<PermissionAuthorizationHandler> logger)
    : AuthorizationHandler<PermissionRequirement>
{
    // Mirrors TicketingService/HSEService's PermissionAuthorizationHandler hierarchy shape.
    // The whistleblower.* pair is deliberately its OWN isolated hierarchy — general
    // compliance.read/write does NOT grant it — per COMP-003's explicit "restricted access"
    // requirement. Only system.admin or the specific whistleblower permission unlocks it.
    private static readonly Dictionary<string, string[]> _hierarchy = new(StringComparer.OrdinalIgnoreCase)
    {
        ["compliance.read"]    = ["compliance.read", "compliance.write", "compliance.delete", "compliance.approve", "system.admin"],
        ["compliance.write"]   = ["compliance.write", "compliance.delete", "compliance.approve", "system.admin"],
        ["compliance.delete"]  = ["compliance.delete", "system.admin"],
        ["compliance.approve"] = ["compliance.approve", "system.admin"],
        ["compliance.whistleblower.read"]  = ["compliance.whistleblower.read", "compliance.whistleblower.write", "system.admin"],
        ["compliance.whistleblower.write"] = ["compliance.whistleblower.write", "system.admin"],
        // Statutory Compliance Calendar (STAT-001..010) — its own hierarchy, not folded into
        // compliance.*: STAT-002 explicitly wants annual-return reminders routed to MD/Company
        // Secretary via the narrower "statutory.approve" tier, separate from the general
        // "statutory.read" every calendar viewer holds.
        ["statutory.read"]    = ["statutory.read", "statutory.write", "statutory.approve", "system.admin"],
        ["statutory.write"]   = ["statutory.write", "statutory.approve", "system.admin"],
        ["statutory.approve"] = ["statutory.approve", "system.admin"],
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

        if (userPermissions.Contains("system.admin"))
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
