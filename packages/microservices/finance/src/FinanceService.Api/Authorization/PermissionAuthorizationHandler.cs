using Microsoft.AspNetCore.Authorization;

namespace FinanceService.Api.Authorization;

/// <summary>
/// Permission checking for finance.
///
/// <para>This service had none. Twelve of the other thirteen carry a handler and between 6 and 244
/// <c>[Authorize(Policy = …)]</c> sites; finance carried <b>one bare [Authorize]</b> on
/// <c>BaseFinanceController</c> and nothing else, so every one of its 79 endpoints was open to any
/// authenticated user in the tenant — customers, invoices, receipts, VAT filings, the chart of
/// accounts, budgets, bank reconciliation, month-end and the journals themselves (#277).</para>
///
/// <para>The gateway does not close this: it authenticates and, by design, never authorises — every
/// one of its routes is <c>Anonymous</c> or <c>Default</c>. And the frontend hiding the module is not
/// a control, because the API is reachable directly.</para>
///
/// <para><b>The hierarchy below is copied verbatim from
/// <c>apps/lante_frontend/src/utils/permissions.js</c></b>, not from a sibling service. The thirteen
/// backend copies all disagree with each other (#204) and several disagree with the frontend (#271),
/// so "match a neighbour" would have meant picking a side at random. Matching the frontend is the one
/// choice that makes the UI's promises true, and
/// <c>scripts/ci/validate_permission_hierarchy.py</c> now enforces it — finance is added to that
/// script's OWNERS in the same change, so this copy cannot drift without failing the build.</para>
/// </summary>
public class PermissionAuthorizationHandler(ILogger<PermissionAuthorizationHandler> logger)
    : AuthorizationHandler<PermissionRequirement>
{
    private static readonly Dictionary<string, string[]> _hierarchy = new(StringComparer.OrdinalIgnoreCase)
    {
        ["finance.read"]    = ["finance.read", "finance.write", "finance.approve", "finance.reports", "system.admin"],
        ["finance.write"]   = ["finance.write", "finance.approve", "system.admin"],
        ["finance.approve"] = ["finance.approve", "system.admin"],
        ["finance.reports"] = ["finance.reports", "finance.approve", "system.admin"],
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

        // system.admin is tenant-wide admin, and finance is a tenant-scoped module, so the bypass
        // applies here. It deliberately does NOT apply in licensing, which is platform-wide — see that
        // service's handler and #271.
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
