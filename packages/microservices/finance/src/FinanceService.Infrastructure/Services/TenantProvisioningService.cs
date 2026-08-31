using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using FinanceService.Infrastructure.Data;

namespace FinanceService.Infrastructure.Services;

/// <summary>Outcome of provisioning this service's tenant schema.</summary>
public record ProvisioningResult(bool Success, string Schema, string? Error);

/// <summary>Creates and migrates the finance tables into a tenant's Postgres schema.</summary>
public interface ITenantProvisioningService
{
    Task<ProvisioningResult> ProvisionAsync(string schema, CancellationToken cancellationToken = default);
}

public partial class TenantProvisioningService : ITenantProvisioningService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<TenantProvisioningService> _logger;

    public TenantProvisioningService(IConfiguration configuration, ILogger<TenantProvisioningService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<ProvisioningResult> ProvisionAsync(string schema, CancellationToken cancellationToken = default)
    {
        if (!IsSafeSchema(schema))
            return new ProvisioningResult(false, schema, $"Unsafe schema name '{schema}'.");

        try
        {
            await using var ctx = BuildTenantContext(schema);
            string? grantError;

            // Serialize concurrent provisioning attempts for the same schema — see
            // UserService/LicenseService's TenantProvisioningService for the full rationale
            // (every pod re-running provisioning on startup can otherwise race a rolling deploy).
            await using var lockConn = new NpgsqlConnection(GetBaseConnectionString());
            await lockConn.OpenAsync(cancellationToken);
            var lockKey = $"finance-provision:{schema}";
            await using (var lockCmd = lockConn.CreateCommand())
            {
                lockCmd.CommandText = "SELECT pg_advisory_lock(hashtextextended(@key, 0))";
                lockCmd.Parameters.AddWithValue("key", lockKey);
                await lockCmd.ExecuteNonQueryAsync(cancellationToken);
            }
            try
            {
#pragma warning disable EF1002 // schema name is validated by IsSafeSchema above before use — not user input, and identifiers can't be parameterized via ExecuteSqlAsync anyway.
                await ctx.Database.ExecuteSqlRawAsync($"CREATE SCHEMA IF NOT EXISTS \"{schema}\"", cancellationToken);
#pragma warning restore EF1002
                await ctx.Database.MigrateAsync(cancellationToken);
                await DatabaseSeeder.SeedAsync(ctx);
                grantError = await GrantAppRoleAsync(ctx, schema, cancellationToken);
            }
            finally
            {
                await using var unlockCmd = lockConn.CreateCommand();
                unlockCmd.CommandText = "SELECT pg_advisory_unlock(hashtextextended(@key, 0))";
                unlockCmd.Parameters.AddWithValue("key", lockKey);
                await unlockCmd.ExecuteNonQueryAsync(cancellationToken);
            }

            if (grantError != null)
            {
                _logger.LogError("Provisioned schema {Schema} but role grant failed: {Error}", schema, grantError);
                return new ProvisioningResult(false, schema, grantError);
            }

            _logger.LogInformation("Provisioned finance schema {Schema}", schema);
            return new ProvisioningResult(true, schema, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed provisioning finance schema {Schema}", schema);
            return new ProvisioningResult(false, schema, ex.Message);
        }
    }

    private string GetBaseConnectionString()
        => _configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection not configured.");

    private TenantFinanceDbContext BuildTenantContext(string schema)
    {
        var baseConnectionString = _configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection not configured.");

        var connectionString = new NpgsqlConnectionStringBuilder(baseConnectionString) { SearchPath = schema }.ConnectionString;

        var options = new DbContextOptionsBuilder<TenantFinanceDbContext>()
            .UseNpgsql(connectionString, o =>
            {
                o.MigrationsAssembly("FinanceService.Infrastructure");
                // Pin the history table to the tenant schema so EF's existence check and its SELECT
                // both target the same schema (an unqualified check can resolve to public.__EFMigrationsHistory).
                o.MigrationsHistoryTable("__EFMigrationsHistory", schema);
            })
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning))
            .Options;

        return new TenantFinanceDbContext(options);
    }

    /// <summary>Grants the runtime app role (qalicore_app) access to the provisioned schema so
    /// search_path resolves it at request time. Best-effort.</summary>
    private async Task<string?> GrantAppRoleAsync(TenantFinanceDbContext ctx, string schema, CancellationToken ct)
    {
        var appRole = _configuration["ProvisioningAppRole"] ?? "qalicore_app";
        if (!SafeSchemaRegex().IsMatch(appRole)) return null;
        try
        {
#pragma warning disable EF1002 // appRole is validated by SafeSchemaRegex above before use — not user input, and identifiers can't be parameterized via ExecuteSqlAsync anyway.
            var roleExists = await ctx.Database
                .SqlQueryRaw<int>($"SELECT 1 AS \"Value\" FROM pg_roles WHERE rolname = '{appRole}'")
                .AnyAsync(ct);
#pragma warning restore EF1002
            if (!roleExists)
            {
                return $"App role '{appRole}' not found.";
            }
#pragma warning disable EF1002 // schema and appRole are validated by IsSafeSchema/SafeSchemaRegex above before use — not user input, and identifiers can't be parameterized via ExecuteSqlAsync anyway.
            await ctx.Database.ExecuteSqlRawAsync($"GRANT USAGE ON SCHEMA \"{schema}\" TO {appRole}", ct);
            await ctx.Database.ExecuteSqlRawAsync($"GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA \"{schema}\" TO {appRole}", ct);
            await ctx.Database.ExecuteSqlRawAsync($"GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA \"{schema}\" TO {appRole}", ct);
            await ctx.Database.ExecuteSqlRawAsync($"ALTER DEFAULT PRIVILEGES IN SCHEMA \"{schema}\" GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO {appRole}", ct);
#pragma warning restore EF1002
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to grant {Role} on {Schema}; tenant may be unreadable at runtime.", appRole, schema);
            return ex.Message;
        }
    }

    private static bool IsSafeSchema(string schema)
        => !string.IsNullOrWhiteSpace(schema) && SafeSchemaRegex().IsMatch(schema);

    [GeneratedRegex("^[a-z_][a-z0-9_]*$")]
    private static partial Regex SafeSchemaRegex();
}
