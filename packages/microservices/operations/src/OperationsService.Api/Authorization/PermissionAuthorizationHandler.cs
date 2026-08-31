using Microsoft.AspNetCore.Authorization;

namespace OperationsService.Api.Authorization;

public class PermissionAuthorizationHandler(ILogger<PermissionAuthorizationHandler> logger)
    : AuthorizationHandler<PermissionRequirement>
{
    private static readonly Dictionary<string, string[]> _hierarchy = new(StringComparer.OrdinalIgnoreCase)
    {
        // The issued-certificate register is owned by Technical but read by CRM too — Technical
        // tracks what it issued, CRM chases the client recalls. Either department's read permission
        // satisfies it; withdrawal remains gated on calibration.sign.
        ["calibration.certificates.read"] = ["calibration.certificates.read",
                                     "operations.read.own", "operations.read.dept", "operations.read.all",
                                     "operations.write", "operations.delete", "operations.approve",
                                     "crm.read.own", "crm.read.dept", "crm.read.all", "crm.write",
                                     "system.admin"],
        ["operations.read.own"]   = ["operations.read.own", "operations.read.dept", "operations.read.all", "operations.write", "operations.delete", "operations.approve", "system.admin"],
        ["operations.read.dept"]  = ["operations.read.dept", "operations.read.all", "system.admin"],
        ["operations.read.all"]   = ["operations.read.all", "system.admin"],
        ["operations.write"]      = ["operations.write", "operations.delete", "operations.approve", "system.admin"],
        ["operations.delete"]     = ["operations.delete", "system.admin"],
        ["operations.approve"]    = ["operations.approve", "system.admin"],
        ["projects.read.own"]     = ["projects.read.own", "projects.write", "projects.delete", "projects.approve", "projects.read.dept", "projects.read.all", "system.admin",
                                     "operations.write", "operations.delete", "operations.approve"],
        ["projects.read.dept"]    = ["projects.read.dept", "projects.write", "projects.delete", "projects.approve", "projects.read.all", "system.admin",
                                     "operations.write", "operations.delete", "operations.approve"],
        ["projects.read.all"]     = ["projects.read.all", "projects.approve", "system.admin"],
        ["projects.write"]        = ["projects.write", "projects.delete", "projects.approve", "system.admin"],
        ["projects.approve"]      = ["projects.approve", "system.admin"],
        // Kept in agreement with finance-service's own copy, the actual enforcer of this
        // permission — this service never checks it locally, but a stale copy here is exactly
        // the class of drift #204 is about.
        ["finance.read"]          = ["finance.read", "finance.write", "finance.approve", "finance.reports", "system.admin"],
        ["finance.write"]         = ["finance.write", "finance.approve", "system.admin"],
        ["finance.approve"]       = ["finance.approve", "system.admin"],
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
