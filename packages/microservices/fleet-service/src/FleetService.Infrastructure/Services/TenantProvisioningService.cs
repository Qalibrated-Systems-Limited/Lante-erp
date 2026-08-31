using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using FleetService.Core.Entities;
using FleetService.Infrastructure.Data;

namespace FleetService.Infrastructure.Services;

/// <summary>Outcome of provisioning this service's tenant schema.</summary>
public record ProvisioningResult(bool Success, string Schema, string? Error);

/// <summary>Creates and migrates the fleet tables into a tenant's Postgres schema.</summary>
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

            // Serialize concurrent provisioning attempts for the same schema — see
            // UserService/LicenseService's TenantProvisioningService for the full rationale
            // (every pod re-running provisioning on startup can otherwise race a rolling deploy).
            await using var lockConn = new NpgsqlConnection(GetBaseConnectionString());
            await lockConn.OpenAsync(cancellationToken);
            var lockKey = $"fleet-provision:{schema}";
            await using (var lockCmd = lockConn.CreateCommand())
            {
                lockCmd.CommandText = "SELECT pg_advisory_lock(hashtextextended(@key, 0))";
                lockCmd.Parameters.AddWithValue("key", lockKey);
                await lockCmd.ExecuteNonQueryAsync(cancellationToken);
            }
            string? grantError;
            try
            {
#pragma warning disable EF1002 // schema name is validated by IsSafeSchema/SafeSchemaRegex above — not user input, and identifiers can't be parameterized via ExecuteSqlAsync anyway.
                await ctx.Database.ExecuteSqlRawAsync($"CREATE SCHEMA IF NOT EXISTS \"{schema}\"", cancellationToken);
#pragma warning restore EF1002
                await ctx.Database.MigrateAsync(cancellationToken);
                await SeedReferenceDataAsync(ctx, cancellationToken);
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
                _logger.LogError("Provisioned fleet schema {Schema} but role grant failed: {Error}", schema, grantError);
                return new ProvisioningResult(false, schema, grantError);
            }

            _logger.LogInformation("Provisioned fleet schema {Schema}", schema);
            return new ProvisioningResult(true, schema, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed provisioning fleet schema {Schema}", schema);
            return new ProvisioningResult(false, schema, ex.Message);
        }
    }

    private string GetBaseConnectionString()
        => _configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection not configured.");

    /// <summary>Seeds sensible starter reference data (trip types, Kenya license classes, a
    /// couple of sample trucks) into a freshly provisioned tenant schema, so Create Trip isn't
    /// broken with empty dropdowns on day one. Idempotent — skips if trip types already exist.
    ///
    /// Deliberately does NOT seed DriverProfile rows: DriverProfile.DriverId must reference a
    /// real user account from the Users/Identity service (see FleetTrucksPage.jsx's
    /// "Fleet Staff" role filter on the frontend) — fabricating one here would create a
    /// dangling reference that looks fine in a dropdown but breaks anywhere driver identity is
    /// resolved cross-service. A tenant's first real driver profile has to come from actually
    /// onboarding a Fleet Staff user, not from this seed step.</summary>
    private static async Task SeedReferenceDataAsync(TenantFleetServiceDbContext ctx, CancellationToken ct)
    {
        if (await ctx.TripTypes.AnyAsync(ct)) return;

        var now = DateTime.UtcNow;

        // Matches FleetService.Api/Program.cs's main-schema seed exactly — these are the three
        // trip types the driver mobile app's card-selection step recognizes by name (see
        // TripCreationWizard._slug in the qalibrated driver app), so tenant and main schemas
        // must stay in sync here.
        var tripTypes = new[]
        {
            new TripType { Name = "Loaded Trip",      Description = "Trip carrying a full load",              Category = TripCategory.Loaded,      EmptyTripOption = EmptyTripOption.NotAllowed, MaterialRequirement = MaterialRequirement.Mandatory, IsActive = true, CreatedByUserId = "system", CreatedAt = now, UpdatedAt = now },
            new TripType { Name = "Empty Trip",       Description = "Trip with no load",                      Category = TripCategory.Empty,       EmptyTripOption = EmptyTripOption.Required,   MaterialRequirement = MaterialRequirement.None,      IsActive = true, CreatedByUserId = "system", CreatedAt = now, UpdatedAt = now },
            new TripType { Name = "Maintenance Trip", Description = "Trip for vehicle servicing or repairs",  Category = TripCategory.Maintenance, EmptyTripOption = EmptyTripOption.Allowed,    MaterialRequirement = MaterialRequirement.None,      IsActive = true, CreatedByUserId = "system", CreatedAt = now, UpdatedAt = now },
        };
        await ctx.TripTypes.AddRangeAsync(tripTypes, ct);

        // Kenya NTSA driving licence classes (common subset).
        var licenseClasses = new[]
        {
            new LicenseClass { Name = "A",  Description = "Motorcycles",                          CreatedAt = now, UpdatedAt = now },
            new LicenseClass { Name = "B",  Description = "Light vehicles up to 3,500 kg",        CreatedAt = now, UpdatedAt = now },
            new LicenseClass { Name = "C1", Description = "Light trucks 3,500–7,500 kg",          CreatedAt = now, UpdatedAt = now },
            new LicenseClass { Name = "C",  Description = "Medium/heavy trucks over 7,500 kg",    CreatedAt = now, UpdatedAt = now },
            new LicenseClass { Name = "CE", Description = "Trucks with trailer",                  CreatedAt = now, UpdatedAt = now },
            new LicenseClass { Name = "D",  Description = "Passenger service vehicles / buses",   CreatedAt = now, UpdatedAt = now },
        };
        await ctx.LicenseClasses.AddRangeAsync(licenseClasses, ct);

        // A couple of clearly-sample trucks so Create Trip has something to select immediately —
        // self-contained (no cross-service identity dependency, unlike DriverProfile above), and
        // just as editable/deletable by the tenant as any other reference-data row.
        var trucks = new[]
        {
            new Truck { LicensePlate = "SAMPLE-001", Make = "Isuzu", Model = "FRR (7 Tonne)", Status = TruckStatus.Active, CreatedAt = now, UpdatedAt = now },
            new Truck { LicensePlate = "SAMPLE-002", Make = "Mitsubishi", Model = "Fuso (10 Tonne)", Status = TruckStatus.Active, CreatedAt = now, UpdatedAt = now },
        };
        await ctx.Trucks.AddRangeAsync(trucks, ct);

        await ctx.SaveChangesAsync(ct);
    }

    private TenantFleetServiceDbContext BuildTenantContext(string schema)
    {
        var baseConnectionString = _configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection not configured.");

        var connectionString = new NpgsqlConnectionStringBuilder(baseConnectionString) { SearchPath = schema }.ConnectionString;

        var options = new DbContextOptionsBuilder<TenantFleetServiceDbContext>()
            .UseNpgsql(connectionString, o =>
            {
                o.MigrationsAssembly("FleetService.Infrastructure");
                o.MigrationsHistoryTable("__EFMigrationsHistory", schema);
            })
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options;

        return new TenantFleetServiceDbContext(options);
    }

    /// <summary>Grants the runtime app role (qalicore_app) access to the provisioned schema so
    /// search_path resolves it at request time. Returns null on success, or an error message —
    /// callers must treat a non-null return as provisioning failure, not a best-effort warning.</summary>
    private async Task<string?> GrantAppRoleAsync(TenantFleetServiceDbContext ctx, string schema, CancellationToken ct)
    {
        var appRole = _configuration["ProvisioningAppRole"] ?? "qalicore_app";
        if (!SafeSchemaRegex().IsMatch(appRole)) return $"Configured app role '{appRole}' failed safety check.";
        try
        {
#pragma warning disable EF1002 // appRole name is validated by SafeSchemaRegex above — not user input, and identifiers can't be parameterized via ExecuteSqlAsync anyway.
            var roleExists = await ctx.Database
                .SqlQueryRaw<int>($"SELECT 1 AS \"Value\" FROM pg_roles WHERE rolname = '{appRole}'")
                .AnyAsync(ct);
#pragma warning restore EF1002
            if (!roleExists)
            {
                return $"App role '{appRole}' not found.";
            }
#pragma warning disable EF1002 // schema and appRole names are validated by IsSafeSchema/SafeSchemaRegex above — not user input, and identifiers can't be parameterized via ExecuteSqlAsync anyway.
            await ctx.Database.ExecuteSqlRawAsync($"GRANT USAGE ON SCHEMA \"{schema}\" TO {appRole}", ct);
            await ctx.Database.ExecuteSqlRawAsync($"GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA \"{schema}\" TO {appRole}", ct);
            await ctx.Database.ExecuteSqlRawAsync($"GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA \"{schema}\" TO {appRole}", ct);
            await ctx.Database.ExecuteSqlRawAsync($"ALTER DEFAULT PRIVILEGES IN SCHEMA \"{schema}\" GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO {appRole}", ct);
#pragma warning restore EF1002
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to grant {Role} on {Schema}; tenant would be unreadable at runtime.", appRole, schema);
            return ex.Message;
        }
    }

    private static bool IsSafeSchema(string schema)
        => !string.IsNullOrWhiteSpace(schema) && SafeSchemaRegex().IsMatch(schema);

    [GeneratedRegex("^[a-z_][a-z0-9_]*$")]
    private static partial Regex SafeSchemaRegex();
}
