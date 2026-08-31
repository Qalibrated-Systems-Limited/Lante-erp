using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using FleetService.Core.Entities;
using FleetService.Infrastructure.Data;

namespace FleetService.Infrastructure.BackgroundServices;

public class TripSyncBackgroundService(IServiceScopeFactory scopeFactory, ILogger<TripSyncBackgroundService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            foreach (var schema in await GetTenantSchemasAsync())
            {
                try
                {
                    await SyncTripMileageForSchemaAsync(schema);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Trip mileage sync failed for schema {Schema} — will retry in 1 hour", schema);
                }
            }
            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }

    private async Task<List<string>> GetTenantSchemasAsync()
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FleetServiceDbContext>();
        return await db.Database
            .SqlQueryRaw<string>("SELECT schema_name FROM information_schema.schemata WHERE schema_name ~ '^tenant_'")
            .ToListAsync();
    }

    // Background jobs have no HTTP request to resolve a tenant schema from — without pinning it
    // explicitly here, this would silently only ever see trips in the shared public schema,
    // never a fully-migrated tenant. See LicenseExpiryBackgroundService for the same fix.
    private async Task SyncTripMileageForSchemaAsync(string schema)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FleetServiceDbContext>();

        await db.Database.OpenConnectionAsync();
#pragma warning disable EF1002 // schema is sourced from information_schema.schemata (GetTenantSchemasAsync) above, not user input, and identifiers can't be parameterized via ExecuteSqlAsync anyway.
        await db.Database.ExecuteSqlRawAsync($"SET search_path TO \"{schema}\", public");
#pragma warning restore EF1002

        // Recalculate TotalMileage for completed trips where EndMileage is set but TotalMileage is 0 or null
        var trips = await db.Trips
            .Where(t => t.Status == TripStatus.Completed
                     && t.EndMileage.HasValue
                     && t.StartMileage.HasValue
                     && (!t.TotalMileage.HasValue || t.TotalMileage == 0)
                     && !t.IsDeleted)
            .ToListAsync();

        foreach (var trip in trips)
        {
            trip.TotalMileage = trip.EndMileage!.Value - trip.StartMileage!.Value;
            trip.UpdatedAt = DateTime.UtcNow;
        }

        if (trips.Any())
        {
            await db.SaveChangesAsync();
            logger.LogInformation("Synced mileage for {Count} trips in {Schema}", trips.Count, schema);
        }

        await db.Database.CloseConnectionAsync();
    }
}
