using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using UserService.Core.Entities;
using UserService.Core.Interfaces.Services;
using UserService.Infrastructure.Data;

namespace UserService.Infrastructure.Services;

/// <summary>See <see cref="IPublicRoleDirectorySync"/>.</summary>
public class PublicRoleDirectorySync : IPublicRoleDirectorySync
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<PublicRoleDirectorySync> _logger;

    public PublicRoleDirectorySync(IConfiguration configuration, ILogger<PublicRoleDirectorySync> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SyncRoleAssignedAsync(string userId, string roleId, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var ctx = BuildPublicContext();

            var roleExistsInPublic = await ctx.Roles.AnyAsync(r => r.Id == roleId, cancellationToken);
            if (!roleExistsInPublic) return; // tenant-custom role — nothing to mirror

            var userExistsInPublic = await ctx.Users.AnyAsync(u => u.Id == userId, cancellationToken);
            if (!userExistsInPublic) return; // no directory pointer for this user

            var alreadyAssigned = await ctx.UserRoles
                .AnyAsync(ur => ur.UserId == userId && ur.RoleId == roleId, cancellationToken);
            if (alreadyAssigned) return;

            ctx.UserRoles.Add(new UserRole { UserId = userId, RoleId = roleId });
            await ctx.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to mirror role {RoleId} assignment for user {UserId} into the public directory.",
                roleId, userId);
        }
    }

    public async Task SyncRoleRemovedAsync(string userId, string roleId, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var ctx = BuildPublicContext();

            var userRole = await ctx.UserRoles
                .FirstOrDefaultAsync(ur => ur.UserId == userId && ur.RoleId == roleId, cancellationToken);
            if (userRole == null) return;

            ctx.UserRoles.Remove(userRole);
            await ctx.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to mirror role {RoleId} removal for user {UserId} into the public directory.",
                roleId, userId);
        }
    }

    private LanteUserServiceDbContext BuildPublicContext()
    {
        var baseConnectionString = _configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection not configured.");

        var connectionString = new NpgsqlConnectionStringBuilder(baseConnectionString) { SearchPath = "public" }.ConnectionString;

        var options = new DbContextOptionsBuilder<LanteUserServiceDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new LanteUserServiceDbContext(options);
    }
}
