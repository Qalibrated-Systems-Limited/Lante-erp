using LicenseService.Core.Entities;

namespace LicenseService.Core.Interfaces.Repositories;

public interface ILicenseRepository : IGenericRepository<License>
{
    Task<License?> GetByTokenAsync(string token);
    Task<IEnumerable<License>> GetByCustomerIdAsync(string customerId);
    Task<IEnumerable<License>> GetByAppIdAsync(string appId);
    Task<IEnumerable<License>> GetActiveAsync();
    Task<IEnumerable<License>> GetExpiringAsync(int withinDays);
    Task<IEnumerable<License>> GetAllWithFiltersAsync(string? customerId, string? appId, bool? active);

    /// <summary>Atomic — sets Revoked/RevokeReason without touching any other column, so a
    /// concurrent RenewAsync's ExpiresAt change (or vice versa) can't be clobbered by a full-record
    /// UpdateAsync overwriting the other operation's in-memory snapshot. Returns false if the
    /// license doesn't exist.</summary>
    Task<bool> TryRevokeAsync(string id, string reason, string revokedBy);

    /// <summary>Atomic — sets ExpiresAt without touching any other column (see TryRevokeAsync).
    /// Returns false if the license doesn't exist, is revoked, or newExpiresAtUtc isn't after the
    /// current ExpiresAt (checked server-side so a stale in-memory read can't approve a renewal
    /// that's since become invalid).</summary>
    Task<bool> TryRenewAsync(string id, DateTime newExpiresAtUtc, string renewedBy);
}
