using LicenseService.Core.Interfaces.Services;
using LicenseService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LicenseService.Infrastructure.BackgroundServices;

// Licensing had zero background jobs before this — GetExpiringAsync only ever ran on-demand
// when someone opened the Licensing page. This proactively pushes an alert into ticketing-
// service's central Alert store, mirroring FleetService's driver-license job.
public class LicenseExpiryBackgroundService(IServiceScopeFactory scopeFactory, ILogger<LicenseExpiryBackgroundService> logger)
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
        var db = scope.ServiceProvider.GetRequiredService<LanteLicenseDbContext>();
        return await db.Database
            .SqlQueryRaw<string>("SELECT schema_name FROM information_schema.schemata WHERE schema_name ~ '^tenant_'")
            .ToListAsync();
    }

    // Background jobs have no HTTP request to resolve a tenant schema from — without pinning it
    // explicitly, this would only ever see the "public"/"licensing" fallback, never a fully-
    // migrated tenant. See TicketingService.SLABackgroundService for the same fix, first found there.
    private async Task CheckExpiringLicensesForSchemaAsync(string schema)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LanteLicenseDbContext>();
        var ticketingClient = scope.ServiceProvider.GetRequiredService<ITicketingServiceClient>();

        await db.Database.OpenConnectionAsync();
#pragma warning disable EF1002 // schema is sourced from information_schema.schemata (GetTenantSchemasAsync above, filtered to '^tenant_') — not user input, and identifiers can't be parameterized via ExecuteSqlAsync anyway.
        await db.Database.ExecuteSqlRawAsync($"SET search_path TO \"{schema}\", public, licensing");
#pragma warning restore EF1002

        var warningDate = DateTime.UtcNow.AddDays(30);
        var expiring = await db.Licenses
            .Where(l => !l.Revoked && !l.IsDeleted && l.ExpiresAt <= warningDate)
            .ToListAsync();

        foreach (var license in expiring)
        {
            var daysLeft = (int)(license.ExpiresAt - DateTime.UtcNow).TotalDays;
            var expired = daysLeft <= 0;
            var subject = license.CustomerName ?? license.CustomerId;

            if (expired)
                logger.LogWarning("License {LicenseId} for {Customer}/{AppId} EXPIRED on {Date} ({Schema})", license.Id, subject, license.AppId, license.ExpiresAt, schema);
            else
                logger.LogWarning("License {LicenseId} for {Customer}/{AppId} expires in {Days} days ({Schema})", license.Id, subject, license.AppId, daysLeft, schema);

            await ticketingClient.CreateAlertAsync(
                tenantSchema: schema,
                source: "LicenseExpiry",
                severity: expired ? "Critical" : "Warning",
                title: expired
                    ? $"License expired — {subject} ({license.AppId})"
                    : $"License expiring within {ExpiryBand(daysLeft)} — {subject} ({license.AppId})",
                message: expired
                    ? $"The {license.AppId} license for {subject} expired on {license.ExpiresAt:d}."
                    : $"The {license.AppId} license for {subject} expires on {license.ExpiresAt:d}.");
        }

        if (expiring.Count > 0)
            logger.LogInformation("{Count} licenses expiring/expired in {Schema}", expiring.Count, schema);

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
