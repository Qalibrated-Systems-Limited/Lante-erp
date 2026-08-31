using Microsoft.AspNetCore.Authorization;

namespace HrService.Api.Authorization;

public class PermissionAuthorizationHandler(ILogger<PermissionAuthorizationHandler> logger)
    : AuthorizationHandler<PermissionRequirement>
{
    private static readonly Dictionary<string, string[]> _hierarchy = new(StringComparer.OrdinalIgnoreCase)
    {
        ["hr.read.own"]   = ["hr.read.own", "hr.read.dept", "hr.read.all", "hr.write", "hr.delete", "hr.approve", "system.admin"],
        ["hr.read.dept"]  = ["hr.read.dept", "hr.read.all", "system.admin"],
        ["hr.read.all"]   = ["hr.read.all", "system.admin"],
        ["hr.write"]      = ["hr.write", "hr.delete", "hr.approve", "system.admin"],
        ["hr.delete"]     = ["hr.delete", "system.admin"],
        ["hr.approve"]    = ["hr.approve", "system.admin"],

        // Payroll is ring-fenced from the rest of HR: salary structures, payroll runs and payslips are
        // visible to payroll staff and the MD, not to everyone who can read employee records.
        ["hr.payroll.read"]    = ["hr.payroll.read", "hr.payroll.write", "hr.payroll.approve", "system.admin"],
        ["hr.payroll.write"]   = ["hr.payroll.write", "hr.payroll.approve", "system.admin"],
        ["hr.payroll.approve"] = ["hr.payroll.approve", "system.admin"],

        // Line-manager scope — approving own team's leave, appraisals and overtime without full HR rights.
        ["hr.manager"]    = ["hr.manager", "hr.write", "hr.delete", "hr.approve", "system.admin"],

        // Finance reads used by the H6 payroll posting and H7 L&D budget seams. Kept in
        // agreement with finance-service's own copy, the actual enforcer of this permission —
        // this service never checks it locally, but a stale copy here is exactly the class of
        // drift #204 is about.
        ["finance.read"]    = ["finance.read", "finance.write", "finance.approve", "finance.reports", "system.admin"],
        ["finance.write"]   = ["finance.write", "finance.approve", "system.admin"],
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
