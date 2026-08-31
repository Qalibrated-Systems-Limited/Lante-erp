using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using ComplianceService.Infrastructure.Data;

namespace ComplianceService.Infrastructure.Services;

/// <summary>Outcome of provisioning this service's tenant schema.</summary>
public record ProvisioningResult(bool Success, string Schema, string? Error);

/// <summary>
/// Creates and migrates Compliance's tables into a tenant's Postgres schema. Copied from
/// HSEService/TicketingService's TenantProvisioningService.
/// </summary>
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

            // Serialize concurrent provisioning attempts for the same schema — e.g. every pod
            // re-running provisioning for every existing tenant schema on startup, racing a
            // rolling deploy or another pod doing the same. Without this, two callers can both
            // run CREATE SCHEMA IF NOT EXISTS + MigrateAsync concurrently against the same schema,
            // throwing duplicate_schema errors or interleaving DDL from two migration runs.
            // Matches UserService/LicenseService's TenantProvisioningService.
            await using var lockConn = new NpgsqlConnection(GetBaseConnectionString());
            await lockConn.OpenAsync(cancellationToken);
            var lockKey = $"compliance-provision:{schema}";
            await using (var lockCmd = lockConn.CreateCommand())
            {
                lockCmd.CommandText = "SELECT pg_advisory_lock(hashtextextended(@key, 0))";
                lockCmd.Parameters.AddWithValue("key", lockKey);
                await lockCmd.ExecuteNonQueryAsync(cancellationToken);
            }
            string? grantError;
            try
            {
#pragma warning disable EF1002 // schema validated by IsSafeSchema (SafeSchemaRegex) above — a SQL identifier, not user data, and identifiers can't be parameterized via ExecuteSqlAsync anyway.
                await ctx.Database.ExecuteSqlRawAsync($"CREATE SCHEMA IF NOT EXISTS \"{schema}\"", cancellationToken);
#pragma warning restore EF1002
                await ctx.Database.MigrateAsync(cancellationToken);
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

            _logger.LogInformation("Provisioned Compliance schema {Schema}", schema);
            return new ProvisioningResult(true, schema, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed provisioning Compliance schema {Schema}", schema);
            return new ProvisioningResult(false, schema, ex.Message);
        }
    }

    private string GetBaseConnectionString()
        => _configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection not configured.");

    private TenantComplianceDbContext BuildTenantContext(string schema)
    {
        var baseConnectionString = _configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection not configured.");

        var connectionString = new NpgsqlConnectionStringBuilder(baseConnectionString)
        {
            SearchPath = schema
        }.ConnectionString;

        var options = new DbContextOptionsBuilder<TenantComplianceDbContext>()
            .UseNpgsql(connectionString, o =>
            {
                o.MigrationsAssembly("ComplianceService.Infrastructure");
                o.MigrationsHistoryTable("__EFMigrationsHistory", schema);
            })
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options;

        return new TenantComplianceDbContext(options);
    }

    /// <summary>Grants the runtime app role access to the freshly provisioned schema — without this,
    /// search_path silently drops the schema and queries fall through to public. Best-effort.</summary>
    private async Task<string?> GrantAppRoleAsync(TenantComplianceDbContext ctx, string schema, CancellationToken ct)
    {
        var appRole = _configuration["ProvisioningAppRole"] ?? "qalicore_app";
        if (!SafeSchemaRegex().IsMatch(appRole)) return null;
        try
        {
#pragma warning disable EF1002 // appRole validated by SafeSchemaRegex above and schema by IsSafeSchema in ProvisionAsync — both are SQL identifiers, not user data, and identifiers can't be parameterized via ExecuteSqlAsync/SqlQuery anyway.
            var roleExists = await ctx.Database
                .SqlQueryRaw<int>($"SELECT 1 AS \"Value\" FROM pg_roles WHERE rolname = '{appRole}'")
                .AnyAsync(ct);
            if (!roleExists)
            {
                return $"App role '{appRole}' not found.";
            }
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
