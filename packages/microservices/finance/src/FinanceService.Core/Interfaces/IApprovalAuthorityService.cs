namespace FinanceService.Core.Interfaces;

/// The caller's authorisation facts, lifted from the JWT (roles + company-admin flag).
public record ApprovalContext(IReadOnlyCollection<string> Roles, bool IsCompanyAdmin)
{
    public static readonly ApprovalContext None = new(Array.Empty<string>(), false);
}

/// Enforces the payment-authority matrix: does the caller hold a role senior enough to approve
/// at the required tier? Gated by Finance:EnforceApprovalAuthority so it can be switched on only
/// once the tenant's finance.* roles exist (company admins always pass).
public interface IApprovalAuthorityService
{
    bool Enabled { get; }
    /// Seniority rank of a role name (0 = unknown/none, higher = more authority).
    int RankOf(string? role);
    int CallerRank(ApprovalContext ctx);
    /// Throws UnauthorizedAccessException (→ 403) when enforcement is on and the caller is too junior.
    void Ensure(ApprovalContext ctx, string requiredRole);
}
