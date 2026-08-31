using FinanceService.Infrastructure.Data;
using FinanceService.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace FinanceService.Infrastructure.BackgroundServices;

/// Mirrors FleetService's LicenseExpiryBackgroundService pattern: a daily tick that, on the 1st of
/// the month, runs depreciation for every tenant schema. finance-service has no HttpContext here, so
/// each tenant gets its own schema-pinned DbContextOptions (same approach Program.cs uses at startup)
/// rather than going through the request-scoped TenantDbConnectionInterceptor. RunDepreciationAsync
/// is itself idempotent, so a missed/duplicate tick is harmless — this is a backfill safety net, not
/// the only way depreciation gets posted (the "Run Depreciation" button covers manual/backfill runs).
public class MonthlyDepreciationBackgroundService(IConfiguration config, ILogger<MonthlyDepreciationBackgroundService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            if (DateTime.UtcNow.Day == 1)
            {
                var period = DateTime.UtcNow.ToString("yyyy-MM");
                foreach (var schema in await GetTenantSchemasAsync())
                {
                    try
                    {
                        await RunForSchemaAsync(schema, period);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Monthly depreciation run failed for schema {Schema}, period {Period}", schema, period);
                    }
                }
            }
            await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
        }
    }

    // Background jobs have no HTTP request to resolve a tenant schema from, so schemas are
    // discovered directly rather than relying on config (a hardcoded fallback here would silently
    // only ever run depreciation for whichever tenant it was written against — see reporting on why
    // that's the exact bug class this repo has hit before).
    private async Task<string[]> GetTenantSchemasAsync()
    {
        var connString = config.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection not configured.");
        await using var conn = new NpgsqlConnection(connString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT schema_name FROM information_schema.schemata WHERE schema_name LIKE 'tenant\\_%' ESCAPE '\\'", conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        var schemas = new List<string>();
        while (await reader.ReadAsync())
            schemas.Add(reader.GetString(0));
        return schemas.ToArray();
    }

    private async Task RunForSchemaAsync(string schema, string period)
    {
        var connString = config.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection not configured.");
        var schemaConn = new NpgsqlConnectionStringBuilder(connString) { SearchPath = schema }.ConnectionString;
        var options = new DbContextOptionsBuilder<TenantFinanceDbContext>()
            .UseNpgsql(schemaConn, o => o.MigrationsAssembly("FinanceService.Infrastructure"))
            .Options;
        await using var db = new TenantFinanceDbContext(options);
        var journals = new JournalService(db);
        var depreciation = new DepreciationService(db, journals);
        var result = await depreciation.RunDepreciationAsync(period, "system");
        if (!result.AlreadyRun)
            logger.LogInformation("Depreciation posted for {Schema} {Period}: {Count} assets, Kshs {Total:N2}",
                schema, period, result.AssetsProcessed, result.TotalCharge);
    }
}
