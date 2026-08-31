using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using FleetService.Core.Interfaces;
using FleetService.Infrastructure.Data;

namespace FleetService.Infrastructure.BackgroundServices;

public class LicenseExpiryBackgroundService(IServiceScopeFactory scopeFactory, ILogger<LicenseExpiryBackgroundService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Initial delay so DB migrations complete before first run
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            foreach (var schema in await GetTenantSchemasAsync())
            {
                try
                {
                    await CheckExpiringLicensesForSchemaAsync(schema);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "License expiry check failed for schema {Schema} — will retry in 24 hours", schema);
                }
            }
            await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
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
    // explicitly here, this would silently only ever see drivers in the shared public schema,
    // never a fully-migrated tenant. See TicketingService's SLABackgroundService for the same fix.
    private async Task CheckExpiringLicensesForSchemaAsync(string schema)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FleetServiceDbContext>();
        var ticketingClient = scope.ServiceProvider.GetRequiredService<ITicketingServiceClient>();

        await db.Database.OpenConnectionAsync();
#pragma warning disable EF1002 // schema is sourced from information_schema.schemata (GetTenantSchemasAsync) above, not user input, and identifiers can't be parameterized via ExecuteSqlAsync anyway.
        await db.Database.ExecuteSqlRawAsync($"SET search_path TO \"{schema}\", public");
#pragma warning restore EF1002

        var warningDate = DateTime.UtcNow.AddDays(30);
        var expiring = await db.DriverProfiles
            .Where(d => d.IsCurrent && !d.IsDeleted && d.LicenseExpiryDate <= warningDate)
            .ToListAsync();

        foreach (var profile in expiring)
        {
            var daysLeft = (int)(profile.LicenseExpiryDate - DateTime.UtcNow).TotalDays;
            var expired = daysLeft <= 0;

            if (expired)
                logger.LogWarning("Driver {DriverId} license EXPIRED on {Date} ({Schema})", profile.DriverId, profile.LicenseExpiryDate, schema);
            else
                logger.LogWarning("Driver {DriverId} license expires in {Days} days ({Date}) ({Schema})", profile.DriverId, daysLeft, profile.LicenseExpiryDate, schema);

            await ticketingClient.CreateAlertAsync(
                tenantSchema: schema,
                source: "DriverLicense",
                severity: expired ? "Critical" : "Warning",
                title: expired
                    ? $"Driver license expired — {profile.FullName}"
                    : $"Driver license expiring within {ExpiryBand(daysLeft)} — {profile.FullName}",
                message: expired
                    ? $"{profile.FullName}'s driving license ({profile.LicenseNumber}) expired on {profile.LicenseExpiryDate:d}."
                    : $"{profile.FullName}'s driving license ({profile.LicenseNumber}) expires on {profile.LicenseExpiryDate:d}.");
        }

        if (expiring.Count > 0)
            logger.LogInformation("{Count} drivers with expiring/expired licenses in {Schema}", expiring.Count, schema);

        await db.Database.CloseConnectionAsync();
    }

    /// <summary>
    /// The escalation band a licence falls into, used in the alert TITLE so the dedup key is stable.
    ///
    /// <para>Titles previously embedded the raw day count, and ticketing dedupes alerts on
    /// (TenantId, Source, Title) — so the key was time-dependent. Each pod starts its own
    /// <c>Task.Delay(24h)</c> loop 30 seconds after boot, giving replicas a PERMANENT offset, and any
    /// licence whose day boundary falls inside that offset computes 30 on one replica and 29 on the
    /// other. Different title, different key, two alerts and two notifications — every day, not
    /// occasionally (#219).</para>
    ///
    /// <para>Banding keeps the escalation the raw count was there for — a fresh, louder alert as the date
    /// approaches — while making the key identical on every replica within a band. The exact date still
    /// goes in the message, where it is informative rather than load-bearing.</para>
    /// </summary>
    internal static string ExpiryBand(int daysLeft) => daysLeft switch
    {
        <= 0  => "expired",
        <= 1  => "1 day",
        <= 7  => "7 days",
        <= 14 => "14 days",
        _     => "30 days",
    };
}
