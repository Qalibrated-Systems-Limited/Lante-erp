using Microsoft.AspNetCore.Authorization;

namespace SubcontractsService.Api.Authorization;

public class PermissionAuthorizationHandler(ILogger<PermissionAuthorizationHandler> logger)
    : AuthorizationHandler<PermissionRequirement>
{
    // subcontracts.* only, since every other service's own permissions are enforced by that
    // service, not this one. subcontracts.approve covers PQQ/award/mobilization sign-off
    // (the "Head of Projects" tier per SUB-002/SUB-004/SUB-005).
    private static readonly Dictionary<string, string[]> _hierarchy = new(StringComparer.OrdinalIgnoreCase)
    {
        ["subcontracts.read"]    = ["subcontracts.read", "subcontracts.write", "subcontracts.delete", "subcontracts.approve", "system.admin"],
        ["subcontracts.write"]   = ["subcontracts.write", "subcontracts.delete", "subcontracts.approve", "system.admin"],
        ["subcontracts.approve"] = ["subcontracts.approve", "system.admin"],
        ["subcontracts.delete"]  = ["subcontracts.delete", "system.admin"],
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
