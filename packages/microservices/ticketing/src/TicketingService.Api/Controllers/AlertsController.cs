using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TicketingService.Core.Interfaces.Services;

namespace TicketingService.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/alerts")]
[Authorize]
public class AlertsController(IAlertService alertService) : ControllerBase
{
    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    // Alert is a shared table spanning every tenant, so every action below must be scoped to the
    // caller's own tenant schema (carried in the JWT "schema" claim) — "CanSeeAllAlerts" only
    // toggles between "everyone's alerts in MY tenant" vs "just mine, in my tenant", never across
    // tenants. A missing claim (shouldn't happen for a real tenant user) is treated as "no tenant"
    // rather than falling back to unscoped, so it can never accidentally return everyone's data.
    private string CurrentTenantSchema => User.FindFirstValue("schema") ?? string.Empty;

    private HashSet<string> CurrentPermissions => User.Claims
        .Where(c => c.Type == "permission")
        .Select(c => c.Value)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private bool CanSeeAllAlerts(HashSet<string> perms) => perms.Contains("system.admin") || perms.Contains("alerts.read.all");

    // Visibility, in order: system.admin/alerts.read.all see everything (legacy blanket gate,
    // kept for ops); otherwise an alert is visible if it's personally assigned to the caller, OR
    // it names a RequiredPermission the caller holds (domain owners — e.g. a Safety Manager with
    // hse.read sees HSE alerts without needing blanket alerts.read.all), OR — only for alerts with
    // NEITHER an assignee nor a RequiredPermission (undifferentiated legacy sources like SLA
    // breaches) — the caller holds the blanket gate. A RequiredPermission alert is NEVER shown to
    // someone who merely holds alerts.read.all but not that specific permission, which is the
    // whole point: domain-restricted alerts (compliance data breaches, HSE incidents, ...) don't
    // leak to every alerts-wide viewer just because they can see the undifferentiated ones.
    private static bool CanSee(TicketingService.Core.Entities.Alert a, HashSet<string> perms, string userId, bool canSeeAll)
    {
        if (perms.Contains("system.admin")) return true;
        if (a.AssignedToUserId == userId) return true;
        if (!string.IsNullOrEmpty(a.RequiredPermission)) return perms.Contains(a.RequiredPermission);
        return canSeeAll;
    }

    [HttpGet]
    public async Task<IActionResult> GetOpen()
    {
        if (string.IsNullOrEmpty(CurrentTenantSchema)) return Ok(new { data = Array.Empty<object>() });

        var perms = CurrentPermissions;
        var canSeeAll = CanSeeAllAlerts(perms);
        var alerts = (await alertService.GetOpenAsync(CurrentTenantSchema))
            .Where(a => CanSee(a, perms, CurrentUserId, canSeeAll));

        var data = alerts.Select(a => new
        {
            a.Id, a.Source, a.Severity, a.Title, a.Message,
            a.TicketId, a.TicketTitle, a.AssignedToUserId, a.RequiredPermission,
            a.IsSeen, a.SeenBy, a.SeenAt,
            a.IsAcknowledged, a.AcknowledgedBy, a.AcknowledgedAt,
            a.CreatedAt,
        });
        return Ok(new { data });
    }

    // Full history (open + acknowledged) — same visibility rule as the open list, so past
    // alerts don't leak to people who couldn't see them while they were open.
    [HttpGet("history")]
    public async Task<IActionResult> GetHistory()
    {
        if (string.IsNullOrEmpty(CurrentTenantSchema)) return Ok(new { data = Array.Empty<object>() });

        var perms = CurrentPermissions;
        var canSeeAll = CanSeeAllAlerts(perms);
        var alerts = (await alertService.GetHistoryAsync(CurrentTenantSchema))
            .Where(a => CanSee(a, perms, CurrentUserId, canSeeAll));

        var data = alerts.Select(a => new
        {
            a.Id, a.Source, a.Severity, a.Title, a.Message,
            a.TicketId, a.TicketTitle, a.AssignedToUserId, a.RequiredPermission,
            a.IsSeen, a.SeenBy, a.SeenAt,
            a.IsAcknowledged, a.AcknowledgedBy, a.AcknowledgedAt,
            a.CreatedAt,
        });
        return Ok(new { data });
    }

    [HttpPatch("{id}/seen")]
    public async Task<IActionResult> MarkSeen(string id)
    {
        // Tenant scoping is enforced at the repository query level; the belt-and-braces
        // TenantId comparison stays as defense-in-depth.
        var alert = await alertService.GetByIdAsync(CurrentTenantSchema, id);
        if (alert == null || alert.TenantId != CurrentTenantSchema) return NotFound(new { message = "Alert not found." });

        var perms = CurrentPermissions;
        if (!CanSee(alert, perms, CurrentUserId, CanSeeAllAlerts(perms)))
            return Forbid();

        await alertService.MarkSeenAsync(CurrentTenantSchema, id, CurrentUserId);
        return Ok(new { message = "Alert marked as seen." });
    }

    [HttpPatch("{id}/acknowledge")]
    public async Task<IActionResult> Acknowledge(string id)
    {
        var alert = await alertService.GetByIdAsync(CurrentTenantSchema, id);
        if (alert == null || alert.TenantId != CurrentTenantSchema) return NotFound(new { message = "Alert not found." });

        // Same visibility rule as the list — can't acknowledge an alert you weren't allowed to see.
        var perms = CurrentPermissions;
        if (!CanSee(alert, perms, CurrentUserId, CanSeeAllAlerts(perms)))
            return Forbid();

        await alertService.AcknowledgeAsync(CurrentTenantSchema, id, CurrentUserId);
        return Ok(new { message = "Alert acknowledged." });
    }
}
