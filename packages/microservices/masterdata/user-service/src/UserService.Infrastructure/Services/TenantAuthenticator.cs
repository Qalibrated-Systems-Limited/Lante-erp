using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using UserService.Core.DTOs.Users;
using UserService.Core.Interfaces.Services;
using UserService.Infrastructure.Data;

namespace UserService.Infrastructure.Services;

/// <summary>
/// See <see cref="ITenantAuthenticator"/>. Binds a <see cref="TenantDbContext"/> to the tenant schema
/// via Postgres search_path and verifies the user's password against that schema's Users table.
/// Roles come from the schema's UserRoles→Roles; permissions/company-admin flag land in Phase 4.
/// </summary>
public class TenantAuthenticator : ITenantAuthenticator
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<TenantAuthenticator> _logger;

    public TenantAuthenticator(IConfiguration configuration, ILogger<TenantAuthenticator> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<UserReadDto?> AuthenticateAsync(
        string schema, string tenantId, string tenantName,
        string email, string password, CancellationToken cancellationToken = default)
    {
        await using var ctx = BuildTenantContext(schema);

        var user = await ctx.Users
            .Include(u => u.UserRoles)
            .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

        if (user is null || user.IsDeleted) return null;
        if (string.IsNullOrEmpty(user.Password) || !BCrypt.Net.BCrypt.Verify(password, user.Password))
            return null;

        return await BuildDtoAsync(ctx, user, schema, tenantId, tenantName, cancellationToken);
    }

    public async Task<UserReadDto?> GetUserByIdAsync(
        string schema, string tenantId, string tenantName,
        string userId, CancellationToken cancellationToken = default)
    {
        await using var ctx = BuildTenantContext(schema);

        var user = await ctx.Users
            .Include(u => u.UserRoles)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null || user.IsDeleted) return null;

        return await BuildDtoAsync(ctx, user, schema, tenantId, tenantName, cancellationToken);
    }

    /// <summary>Resolves roles + permissions from the tenant schema and maps to a UserReadDto.</summary>
    private async Task<UserReadDto> BuildDtoAsync(
        TenantDbContext ctx, UserService.Core.Entities.User user,
        string schema, string tenantId, string tenantName, CancellationToken cancellationToken)
    {
        var roleIds = user.UserRoles.Select(ur => ur.RoleId).ToList();
        var roles = await ctx.Roles
            .Where(r => roleIds.Contains(r.Id))
            .Select(r => r.Name)
            .ToListAsync(cancellationToken);

        // Permissions: union of the user's roles' permissions and any direct user permissions.
        // RolePermission/UserPermission live in the tenant schema; Permission is the global public
        // catalog (cross-schema join, resolved by the search_path-bound connection).
        var rolePerms = await ctx.RolePermissions
            .Where(rp => roleIds.Contains(rp.RoleId))
            .Select(rp => rp.Permission.Name)
            .ToListAsync(cancellationToken);
        var userPerms = await ctx.UserPermissions
            .Where(up => up.UserId == user.Id)
            .Select(up => up.Permission.Name)
            .ToListAsync(cancellationToken);
        var permissions = rolePerms.Concat(userPerms).Distinct().ToList();

        _logger.LogInformation("Tenant-schema user {Email} in {Schema} ({RoleCount} roles, {PermCount} perms)",
            user.Email, schema, roles.Count, permissions.Count);

        return new UserReadDto
        {
            Id               = user.Id,
            FirstName        = user.FirstName,
            LastName         = user.LastName,
            Email            = user.Email,
            MobileNumber     = user.MobileNumber,
            IsActive         = user.IsActive,
            IsFirstLogin     = user.IsFirstLogin,
            TwoFactorEnabled = user.TwoFactorEnabled,
            DepartmentId     = user.DepartmentId,
            Roles            = roles,
            Permissions      = permissions,
            TenantId         = tenantId,
            TenantName       = tenantName,
            SchemaName       = schema,
            BranchId         = user.BranchId,
            IsCompanyAdmin   = user.IsCompanyAdmin,
            CreatedAt        = user.CreatedAt,
            UpdatedAt        = user.UpdatedAt,
        };
    }

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
}
