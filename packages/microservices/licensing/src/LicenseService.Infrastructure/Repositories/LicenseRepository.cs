using LicenseService.Core.Entities;
using LicenseService.Core.Interfaces.Repositories;
using LicenseService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LicenseService.Infrastructure.Repositories;

public class LicenseRepository(LanteLicenseDbContext context)
    : GenericRepository<License>(context), ILicenseRepository
{
    public async Task<License?> GetByTokenAsync(string token)
    {
        return await Context.Licenses
            .IgnoreQueryFilters() // token lookup must work even if soft-deleted (for revoke audit)
            .FirstOrDefaultAsync(l => l.Token == token);
    }

    public async Task<IEnumerable<License>> GetByCustomerIdAsync(string customerId)
    {
        return await Context.Licenses
            .Where(l => l.CustomerId == customerId)
            .OrderByDescending(l => l.IssuedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<License>> GetByAppIdAsync(string appId)
    {
        return await Context.Licenses
            .Where(l => l.AppId == appId)
            .OrderByDescending(l => l.IssuedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<License>> GetActiveAsync()
    {
        var now = DateTime.UtcNow;
        return await Context.Licenses
            .Where(l => !l.Revoked && l.ExpiresAt > now)
            .OrderByDescending(l => l.IssuedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<License>> GetExpiringAsync(int withinDays)
    {
        var now    = DateTime.UtcNow;
        var cutoff = now.AddDays(withinDays);
        return await Context.Licenses
            .Where(l => !l.Revoked && l.ExpiresAt > now && l.ExpiresAt <= cutoff)
            .OrderBy(l => l.ExpiresAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<License>> GetAllWithFiltersAsync(
        string? customerId, string? appId, bool? active)
    {
        var query = Context.Licenses.AsQueryable();

        if (!string.IsNullOrWhiteSpace(customerId))
            query = query.Where(l => l.CustomerId == customerId);

        if (!string.IsNullOrWhiteSpace(appId))
            query = query.Where(l => l.AppId == appId);

        if (active == true)
        {
            var now = DateTime.UtcNow;
            query = query.Where(l => !l.Revoked && l.ExpiresAt > now);
        }
        else if (active == false)
        {
            var now = DateTime.UtcNow;
            query = query.Where(l => l.Revoked || l.ExpiresAt <= now);
        }

        return await query.OrderByDescending(l => l.IssuedAt).ToListAsync();
    }

    public async Task<bool> TryRevokeAsync(string id, string reason, string revokedBy)
    {
        var rows = await Context.Licenses
            .Where(l => l.Id == id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(l => l.Revoked, true)
                .SetProperty(l => l.RevokeReason, reason)
                .SetProperty(l => l.UpdatedBy, revokedBy)
                .SetProperty(l => l.UpdatedAt, DateTime.UtcNow));
        return rows > 0;
    }

    public async Task<bool> TryRenewAsync(string id, DateTime newExpiresAtUtc, string renewedBy)
    {
        var rows = await Context.Licenses
            .Where(l => l.Id == id && !l.Revoked && l.ExpiresAt < newExpiresAtUtc)
            .ExecuteUpdateAsync(s => s
                .SetProperty(l => l.ExpiresAt, newExpiresAtUtc)
                .SetProperty(l => l.UpdatedBy, renewedBy)
                .SetProperty(l => l.UpdatedAt, DateTime.UtcNow));
        return rows > 0;
    }
}
