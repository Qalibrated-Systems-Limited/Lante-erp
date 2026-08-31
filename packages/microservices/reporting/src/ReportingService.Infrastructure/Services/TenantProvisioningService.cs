using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using ReportingService.Infrastructure.Data;

namespace ReportingService.Infrastructure.Services;

/// <summary>Outcome of provisioning this service's tenant schema.</summary>
public record ProvisioningResult(bool Success, string Schema, string? Error);

/// <summary>
/// Creates and migrates Reporting's tables into a tenant's Postgres schema. Copied from
/// Compliance/Subcontracts/HSEService's TenantProvisioningService.
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
#pragma warning disable EF1002 // schema is validated by IsSafeSchema above — not user input, and identifiers can't be parameterized via ExecuteSqlAsync anyway.
            await ctx.Database.ExecuteSqlRawAsync($"CREATE SCHEMA IF NOT EXISTS \"{schema}\"", cancellationToken);
#pragma warning restore EF1002
            await ctx.Database.MigrateAsync(cancellationToken);
            var grantError = await GrantAppRoleAsync(ctx, schema, cancellationToken);

            if (grantError != null)
            {
                _logger.LogError("Provisioned schema {Schema} but role grant failed: {Error}", schema, grantError);
                return new ProvisioningResult(false, schema, grantError);
            }

            _logger.LogInformation("Provisioned Reporting schema {Schema}", schema);
            return new ProvisioningResult(true, schema, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed provisioning Reporting schema {Schema}", schema);
            return new ProvisioningResult(false, schema, ex.Message);
        }
    }

    private TenantReportingDbContext BuildTenantContext(string schema)
    {
        var baseConnectionString = _configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection not configured.");

        var connectionString = new NpgsqlConnectionStringBuilder(baseConnectionString)
        {
            SearchPath = schema
        }.ConnectionString;

        var options = new DbContextOptionsBuilder<TenantReportingDbContext>()
            .UseNpgsql(connectionString, o =>
            {
                o.MigrationsAssembly("ReportingService.Infrastructure");
                o.MigrationsHistoryTable("__EFMigrationsHistory", schema);
            })
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options;

        return new TenantReportingDbContext(options);
    }

    /// <summary>Grants the runtime app role access to the freshly provisioned schema — without this,
    /// search_path silently drops the schema and queries fall through to public. Best-effort.</summary>
    private async Task<string?> GrantAppRoleAsync(TenantReportingDbContext ctx, string schema, CancellationToken ct)
    {
        var appRole = _configuration["ProvisioningAppRole"] ?? "qalicore_app";
        if (!SafeSchemaRegex().IsMatch(appRole)) return null;
        try
        {
#pragma warning disable EF1002 // appRole is validated by SafeSchemaRegex above and schema by IsSafeSchema at the call site — not user input, and identifiers can't be parameterized via ExecuteSqlAsync anyway.
            var roleExists = await ctx.Database
                .SqlQueryRaw<int>($"SELECT 1 AS \"Value\" FROM pg_roles WHERE rolname = '{appRole}'")
                .AnyAsync(ct);
#pragma warning restore EF1002
            if (!roleExists)
            {
                return $"App role '{appRole}' not found.";
            }
#pragma warning disable EF1002 // schema/appRole are validated by IsSafeSchema/SafeSchemaRegex above — not user input, and identifiers can't be parameterized via ExecuteSqlAsync anyway.
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
