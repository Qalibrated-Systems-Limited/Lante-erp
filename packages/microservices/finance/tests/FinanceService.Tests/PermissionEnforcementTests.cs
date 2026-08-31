using System.Security.Claims;
using FinanceService.Api.Authorization;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FinanceService.Tests;

/// <summary>
/// Finance permission enforcement.
///
/// <para>Until #277 this service had none: one bare <c>[Authorize]</c> on <c>BaseFinanceController</c>
/// and not a single policy across 79 endpoints, so any authenticated user in the tenant could read the
/// ledger, the chart of accounts, every customer and every VAT filing, and could raise journals — by
/// calling the API directly. The frontend hid the module, which is not a control.</para>
///
/// <para>These tests exist because the failure mode being fixed is <b>a guard that does not fire</b>
/// (#276). Attributes that read as protection and enforce nothing look exactly like attributes that
/// work, so the handler is asserted directly rather than assumed from the presence of the attribute.</para>
/// </summary>
public class PermissionEnforcementTests
{
    private static AuthorizationHandlerContext ContextFor(string permission, params string[] held)
    {
        var identity = new ClaimsIdentity(
            held.Select(p => new Claim("permission", p)), authenticationType: "TestAuth");
        return new AuthorizationHandlerContext(
            [new PermissionRequirement(permission)], new ClaimsPrincipal(identity), resource: null);
    }

    private static async Task<bool> AllowsAsync(string required, params string[] held)
    {
        var ctx = ContextFor(required, held);
        await new PermissionAuthorizationHandler(
            NullLogger<PermissionAuthorizationHandler>.Instance).HandleAsync(ctx);
        return ctx.HasSucceeded;
    }

    // ── The hole that was open ───────────────────────────────────────────────────

    [Fact]
    public async Task A_user_from_another_module_cannot_read_finance()
    {
        // The exact scenario from #277: a driver holding only fleet.write. Authenticated, legitimate,
        // and previously able to read the entire general ledger through the API.
        (await AllowsAsync("finance.read", "fleet.write")).Should().BeFalse();
        (await AllowsAsync("finance.write", "fleet.write")).Should().BeFalse();
        (await AllowsAsync("finance.approve", "fleet.write")).Should().BeFalse();
    }

    [Fact]
    public async Task Being_merely_authenticated_grants_nothing()
    {
        // Authentication is not authorization. This is what the bare [Authorize] amounted to.
        (await AllowsAsync("finance.read")).Should().BeFalse();
    }

    [Fact]
    public async Task An_unauthenticated_principal_is_refused()
    {
        // No authenticationType => IsAuthenticated is false.
        var ctx = new AuthorizationHandlerContext(
            [new PermissionRequirement("finance.read")],
            new ClaimsPrincipal(new ClaimsIdentity()), resource: null);
        await new PermissionAuthorizationHandler(
            NullLogger<PermissionAuthorizationHandler>.Instance).HandleAsync(ctx);
        ctx.HasSucceeded.Should().BeFalse();
    }

    // ── The hierarchy, which must match the frontend exactly ─────────────────────

    [Theory]
    [InlineData("finance.read")]
    [InlineData("finance.write")]
    [InlineData("finance.approve")]
    [InlineData("finance.reports")]
    public async Task Every_finance_permission_implies_read(string held)
    {
        // Anyone who may act on finance data may see it. Without this a finance.write user could post
        // an invoice and then be refused the list they just added to.
        (await AllowsAsync("finance.read", held)).Should().BeTrue();
    }

    [Fact]
    public async Task Read_does_not_imply_write_and_write_does_not_imply_approve()
    {
        // The whole point of the split. If read implied write, the floor would be the ceiling.
        (await AllowsAsync("finance.write", "finance.read")).Should().BeFalse();
        (await AllowsAsync("finance.approve", "finance.write")).Should().BeFalse();
    }

    [Fact]
    public async Task Approve_implies_write()
    {
        // Someone trusted to approve a payment is trusted to raise one; the reverse is the control.
        (await AllowsAsync("finance.write", "finance.approve")).Should().BeTrue();
    }

    [Fact]
    public async Task Reports_does_not_grant_write_or_approve()
    {
        // An analyst with reporting access must not be able to touch the ledger.
        (await AllowsAsync("finance.write", "finance.reports")).Should().BeFalse();
        (await AllowsAsync("finance.approve", "finance.reports")).Should().BeFalse();
    }

    // ── system.admin ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task System_admin_passes_every_finance_check()
    {
        // system.admin means "admin within your own tenant", and finance is tenant-scoped, so the
        // bypass belongs here. It deliberately does NOT apply in licensing, which is platform-wide
        // (#271) — the two services are meant to differ on exactly this point.
        foreach (var p in new[] { "finance.read", "finance.write", "finance.approve", "finance.reports" })
            (await AllowsAsync(p, "system.admin")).Should().BeTrue();
    }

    // ── Unknown permissions fall back to an exact match, never to allow ──────────

    [Fact]
    public async Task An_unknown_permission_requires_itself_and_is_not_waved_through()
    {
        (await AllowsAsync("finance.invented", "finance.read")).Should().BeFalse();
        (await AllowsAsync("finance.invented", "finance.invented")).Should().BeTrue();
    }

    [Fact]
    public async Task Permission_matching_is_case_insensitive()
    {
        // Claims come from a JWT minted by another service; a casing difference must not become an
        // authorization difference in either direction.
        (await AllowsAsync("finance.read", "FINANCE.WRITE")).Should().BeTrue();
        (await AllowsAsync("FINANCE.APPROVE", "finance.approve")).Should().BeTrue();
    }
}
