using FinanceService.Core.Interfaces;
using Microsoft.Extensions.Configuration;

namespace FinanceService.Infrastructure.Services;

/// Maps role names to the QSL payment-authority ladder and enforces it when enabled. Recognises the
/// canonical tier names plus common aliases; company admins and top-tier roles clear every gate.
public class ApprovalAuthorityService : IApprovalAuthorityService
{
    private const int AdminRank = 99;

    // Normalised role name → rank. Tier names come from the seed (Staff … Managing Director).
    private static readonly Dictionary<string, int> Ranks = new(StringComparer.OrdinalIgnoreCase)
    {
        ["staff"] = 1,
        ["department head"] = 2, ["dept head"] = 2, ["departmenthead"] = 2, ["head of department"] = 2,
        ["finance manager"] = 3, ["financemanager"] = 3, ["fm"] = 3,
        ["cfo"] = 4, ["chief finance officer"] = 4, ["chief financial officer"] = 4,
        ["managing director"] = 5, ["md"] = 5, ["ceo"] = 5, ["managingdirector"] = 5,
        // Anyone with a blanket admin role clears every tier.
        ["admin"] = AdminRank, ["administrator"] = AdminRank, ["system administrator"] = AdminRank,
        ["super admin"] = AdminRank, ["superadmin"] = AdminRank,
    };

    public bool Enabled { get; }
    public ApprovalAuthorityService(IConfiguration config)
        => Enabled = bool.TryParse(config["Finance:EnforceApprovalAuthority"], out var on) && on;

    public int RankOf(string? role)
        => string.IsNullOrWhiteSpace(role) ? 0 : Ranks.TryGetValue(role.Trim(), out var r) ? r : 0;

    public int CallerRank(ApprovalContext ctx)
    {
        if (ctx.IsCompanyAdmin) return AdminRank;
        var max = 0;
        foreach (var role in ctx.Roles) max = Math.Max(max, RankOf(role));
        return max;
    }

    public void Ensure(ApprovalContext ctx, string requiredRole)
    {
        if (!Enabled) return;
        var required = RankOf(requiredRole);
        var caller = CallerRank(ctx);
        if (caller < required)
            throw new UnauthorizedAccessException(
                $"This action requires {requiredRole} authority. Your role is not senior enough — escalate to a {requiredRole} or above.");
    }
}
