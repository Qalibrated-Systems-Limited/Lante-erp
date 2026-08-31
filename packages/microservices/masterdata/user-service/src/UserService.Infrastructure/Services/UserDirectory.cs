using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using UserService.Core.Entities;
using UserService.Core.Interfaces.Repositories;
using UserService.Core.Interfaces.Services;
using UserService.Infrastructure.Data;

namespace UserService.Infrastructure.Services;

/// <summary>
/// Reads/writes the control-plane user directory (public.Users) regardless of the request's tenant
/// search_path. Uses a dedicated DbContext WITHOUT the tenant search_path interceptor and with
/// SearchPath=public pinned on the connection, so invited users always land in public — where
/// single-login (which runs pre-auth with no schema claim) can find them by email.
/// </summary>
public class UserDirectory : IUserDirectory
{
    private readonly string _connectionString;
    private readonly IConfiguration _configuration;
    private readonly ITenantRepository _tenantRepository;
    private readonly ILogger<UserDirectory> _logger;

    public UserDirectory(IConfiguration configuration, ITenantRepository tenantRepository, ILogger<UserDirectory> logger)
    {
        var raw = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection is not configured.");
        _connectionString = new NpgsqlConnectionStringBuilder(raw) { SearchPath = "public" }.ConnectionString;
        _configuration = configuration;
        _tenantRepository = tenantRepository;
        _logger = logger;
    }

    private LanteUserServiceDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<LanteUserServiceDbContext>()
            .UseNpgsql(_connectionString, o => o.MigrationsHistoryTable("__EFMigrationsHistory", "public"))
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning))
            .Options;
        return new LanteUserServiceDbContext(options);
    }

    // Same construction pattern as TenantAuthenticator.BuildTenantContext — kept private/duplicated
    // here rather than shared, matching the existing convention in this codebase.
    private TenantDbContext BuildTenantContext(string schema)
    {
        var baseConnectionString = _configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection not configured.");
        var connectionString = new NpgsqlConnectionStringBuilder(baseConnectionString) { SearchPath = schema }.ConnectionString;
        var options = new DbContextOptionsBuilder<TenantDbContext>()
            .UseNpgsql(connectionString, o => o.MigrationsAssembly("UserService.Infrastructure"))
            .Options;
        return new TenantDbContext(options);
    }

    public async Task CreateInvitedUserAsync(User user, IReadOnlyCollection<string>? roleIds, string? tenantId, string? branchId)
    {
        await using var db = NewContext();
        db.Users.Add(user);

        if (roleIds is { Count: > 0 })
            foreach (var roleId in roleIds)
                db.Set<UserRole>().Add(new UserRole
                {
                    Id = Guid.NewGuid().ToString(), UserId = user.Id, RoleId = roleId,
                    CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
                });

        if (!string.IsNullOrEmpty(tenantId))
            db.Set<UserTenant>().Add(new UserTenant
            {
                Id = Guid.NewGuid().ToString(), UserId = user.Id, TenantId = tenantId,
                BranchId = branchId, IsDefault = true,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            });

        await db.SaveChangesAsync();
    }

    public async Task CreateDirectoryPointerAsync(User user, IReadOnlyCollection<string>? roleIds, string? tenantId)
    {
        await using var db = NewContext();

        // Department/branch are tenant-scoped — never persist them into the public directory row.
        user.DepartmentId = null;
        user.BranchId = null;
        db.Users.Add(user);

        // Only link roles that actually exist in public.Roles (seeded/global). Tenant-custom roles
        // live in the tenant schema and would violate the public FK.
        if (roleIds is { Count: > 0 })
        {
            var publicRoleIds = await db.Set<Role>()
                .Where(r => roleIds.Contains(r.Id))
                .Select(r => r.Id)
                .ToListAsync();
            foreach (var roleId in publicRoleIds)
                db.Set<UserRole>().Add(new UserRole
                {
                    Id = Guid.NewGuid().ToString(), UserId = user.Id, RoleId = roleId,
                    CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
                });
        }

        // Tenant link for single-login email→tenant routing. No BranchId (tenant-scoped FK).
        if (!string.IsNullOrEmpty(tenantId))
            db.Set<UserTenant>().Add(new UserTenant
            {
                Id = Guid.NewGuid().ToString(), UserId = user.Id, TenantId = tenantId,
                BranchId = null, IsDefault = true,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            });

        await db.SaveChangesAsync();
    }

    public async Task<User?> FindByInviteTokenHashAsync(string tokenHash)
    {
        await using var db = NewContext();
        return await db.Users.FirstOrDefaultAsync(u => u.InviteTokenHash == tokenHash && !u.IsDeleted);
    }

    public async Task<User?> AcceptInviteAsync(string tokenHash, string passwordHash)
    {
        await using var db = NewContext();
        var user = await db.Users.FirstOrDefaultAsync(u => u.InviteTokenHash == tokenHash && !u.IsDeleted);
        if (user == null || user.InviteTokenExpiresAt == null || user.InviteTokenExpiresAt < DateTime.UtcNow)
            return null;

        user.Password = passwordHash;
        user.IsActive = true;
        user.IsFirstLogin = false;
        user.InviteTokenHash = null;
        user.InviteTokenExpiresAt = null;
        user.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var userTenant = await db.Set<UserTenant>().FirstOrDefaultAsync(ut => ut.UserId == user.Id && ut.IsDefault && !ut.IsDeleted);
        if (userTenant is not null)
            await SyncPasswordIntoTenantSchemaCoreAsync(user.Id, user.Password!, userTenant.TenantId, isFirstLogin: false, activate: true, clearInviteToken: true);

        return user;
    }

    public async Task SyncUserAuthStateAsync(User user)
    {
        await SyncIntoPublicDirectoryAsync(user);
        await SyncIntoTenantSchemaAsync(user);
    }

    private async Task SyncIntoPublicDirectoryAsync(User user)
    {
        try
        {
            await using var db = NewContext();
            var directoryUser = await db.Users.FindAsync(user.Id);
            if (directoryUser is null) return; // no public pointer for this user — nothing to sync

            ApplyAuthFields(directoryUser, user);
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to sync auth state into public directory for user {UserId}. Whichever schema " +
                "the write actually landed on is current; single-login/invite-by-email against the " +
                "public directory may use stale data until this is retried.", user.Id);
        }
    }

    private async Task SyncIntoTenantSchemaAsync(User user)
    {
        try
        {
            await using var db = NewContext();
            var userTenant = await db.Set<UserTenant>().FirstOrDefaultAsync(ut => ut.UserId == user.Id && ut.IsDefault && !ut.IsDeleted);
            if (userTenant is null) return; // not a schema-per-tenant user — nothing to sync

            var tenant = await _tenantRepository.GetByIdAsync(userTenant.TenantId);
            if (string.IsNullOrEmpty(tenant?.SchemaName)) return;

            await using var tenantDb = BuildTenantContext(tenant.SchemaName);
            var tenantUser = await tenantDb.Users.FindAsync(user.Id);
            if (tenantUser is null) return; // invite predates CreateDirectoryPointerAsync, or that write failed

            ApplyAuthFields(tenantUser, user);
            await tenantDb.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to sync auth state into tenant schema for user {UserId}. Whichever schema the " +
                "write actually landed on is current; this user's tenant-scoped (subdomain) login path " +
                "may use stale data until this is retried.", user.Id);
        }
    }

    private static void ApplyAuthFields(User target, User source)
    {
        target.Email = source.Email;
        target.Password = source.Password;
        target.IsActive = source.IsActive;
        target.IsFirstLogin = source.IsFirstLogin;
        target.TwoFactorEnabled = source.TwoFactorEnabled;
        target.InviteTokenHash = source.InviteTokenHash;
        target.InviteTokenExpiresAt = source.InviteTokenExpiresAt;
        target.UpdatedAt = DateTime.UtcNow;
    }

    // CreateDirectoryPointerAsync writes the user's real record (with password left NULL) into the
    // tenant schema at invite time, and a thin login pointer here in public. Accepting the invite (or
    // completing the first-login update-password flow, or an admin password reset) only updates the
    // public pointer by default — without this, the tenant-schema copy would keep an old password/
    // first-login flag forever, and any tenant-scoped login path could never authenticate the user
    // with their current credentials. Best-effort/non-fatal: the public accept/update above is the
    // source of truth for control-plane (subdomain-less) login and must still succeed even if the
    // tenant schema is unreachable.
    private async Task SyncPasswordIntoTenantSchemaCoreAsync(string userId, string passwordHash, string tenantId, bool isFirstLogin, bool activate, bool clearInviteToken)
    {
        try
        {
            var tenant = await _tenantRepository.GetByIdAsync(tenantId);
            if (string.IsNullOrEmpty(tenant?.SchemaName)) return;

            await using var tenantDb = BuildTenantContext(tenant.SchemaName);
            var tenantUser = await tenantDb.Users.FindAsync(userId);
            if (tenantUser is null) return; // invite predates CreateDirectoryPointerAsync, or that write failed

            tenantUser.Password = passwordHash;
            tenantUser.IsFirstLogin = isFirstLogin;
            if (activate) tenantUser.IsActive = true;
            if (clearInviteToken)
            {
                tenantUser.InviteTokenHash = null;
                tenantUser.InviteTokenExpiresAt = null;
            }
            tenantUser.UpdatedAt = DateTime.UtcNow;
            await tenantDb.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to sync password/first-login status into tenant schema for user {UserId}, tenant {TenantId}. " +
                "The public-schema update succeeded; only the tenant-scoped login path is affected.",
                userId, tenantId);
        }
    }
}
