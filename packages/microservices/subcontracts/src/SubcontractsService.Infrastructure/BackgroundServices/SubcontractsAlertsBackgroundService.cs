using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SubcontractsService.Core.Interfaces.Services;
using SubcontractsService.Infrastructure.Data;

namespace SubcontractsService.Infrastructure.BackgroundServices;

/// <summary>
/// SUB-008: insurance and Tax Compliance Certificate expiry tracking — subcontractors with an
/// insurance or TCC expiry within 30 days are flagged IsRestricted (blocking new awards via
/// AwardWorkflowService), and an alert is raised. Runs every 24 hours, matching HSE/Fleet's
/// cadence for non-time-critical expiry checks.
/// </summary>
public class SubcontractsAlertsBackgroundService(IServiceScopeFactory scopeFactory, ILogger<SubcontractsAlertsBackgroundService> logger)
    : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(24);
    private const int WarningWindowDays = 30;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); // let migrations finish first

        while (!stoppingToken.IsCancellationRequested)
        {
            foreach (var schema in await GetTenantSchemasAsync())
            {
                try
                {
                    await CheckSchemaAsync(schema);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Subcontracts alert check failed for schema {Schema} — will retry next cycle", schema);
                }
            }
            await Task.Delay(CheckInterval, stoppingToken);
        }
    }

    private async Task<List<string>> GetTenantSchemasAsync()
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SubcontractsDbContext>();
        return await db.Database
            .SqlQueryRaw<string>("SELECT schema_name FROM information_schema.schemata WHERE schema_name ~ '^tenant_'")
            .ToListAsync();
    }

    private async Task CheckSchemaAsync(string schema)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SubcontractsDbContext>();
        var ticketingClient = scope.ServiceProvider.GetRequiredService<ITicketingServiceClient>();

        await db.Database.OpenConnectionAsync();
#pragma warning disable EF1002 // schema is sourced from information_schema.schemata (GetTenantSchemasAsync), not user input, and identifiers can't be parameterized via ExecuteSqlAsync anyway.
        await db.Database.ExecuteSqlRawAsync($"SET search_path TO \"{schema}\", public");
#pragma warning restore EF1002

        var now = DateTime.UtcNow;
        var horizon = now.AddDays(WarningWindowDays);
        var subcontractors = await db.Subcontractors.ToListAsync();
        var alertsRaised = 0;

        foreach (var s in subcontractors)
        {
            var insuranceDue = s.InsuranceExpiry.HasValue && s.InsuranceExpiry.Value <= horizon;
            var tccDue = s.TccExpiry.HasValue && s.TccExpiry.Value <= horizon;
            var shouldRestrict = (s.InsuranceExpiry.HasValue && s.InsuranceExpiry.Value <= now)
                                  || (s.TccExpiry.HasValue && s.TccExpiry.Value <= now);

            if (s.IsRestricted != shouldRestrict)
            {
                s.IsRestricted = shouldRestrict;
                db.Subcontractors.Update(s);
            }

            if (insuranceDue)
            {
                var daysLeft = (int)(s.InsuranceExpiry!.Value - now).TotalDays;
                var expired = daysLeft <= 0;
                alertsRaised++;
                await ticketingClient.CreateAlertAsync(schema, "Subcontractor",
                    expired ? "Critical" : "Warning",
                    expired ? $"Insurance expired — {s.Name}" : $"Insurance expiring in {daysLeft} days — {s.Name}",
                    expired ? $"{s.Name}'s insurance expired on {s.InsuranceExpiry.Value:d} — new awards are now restricted."
                            : $"{s.Name}'s insurance expires on {s.InsuranceExpiry.Value:d}.",
                    requiredPermission: "subcontracts.read");
            }

            if (tccDue)
            {
                var daysLeft = (int)(s.TccExpiry!.Value - now).TotalDays;
                var expired = daysLeft <= 0;
                alertsRaised++;
                await ticketingClient.CreateAlertAsync(schema, "Subcontractor",
                    expired ? "Critical" : "Warning",
                    expired ? $"Tax Compliance Certificate expired — {s.Name}" : $"TCC expiring in {daysLeft} days — {s.Name}",
                    expired ? $"{s.Name}'s Tax Compliance Certificate expired on {s.TccExpiry.Value:d} — new awards are now restricted."
                            : $"{s.Name}'s Tax Compliance Certificate expires on {s.TccExpiry.Value:d}.",
                    requiredPermission: "subcontracts.read");
            }
        }

        await db.SaveChangesAsync();

        if (alertsRaised > 0)
            logger.LogInformation("Subcontracts alerts in {Schema}: {Count} insurance/TCC expiry alerts", schema, alertsRaised);

        await db.Database.CloseConnectionAsync();
    }
}
