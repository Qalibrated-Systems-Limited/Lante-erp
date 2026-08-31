using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using HSEService.Core.Interfaces.Services;
using HSEService.Infrastructure.Data;

namespace HSEService.Infrastructure.BackgroundServices;

/// <summary>
/// HSE-005 (training certificate renewal alerts) + HSE-006 (statutory inspection due dates),
/// daily per tenant schema, pushed into ticketing's shared Alerts table. Copied structurally from
/// FleetService's LicenseExpiryBackgroundService — background jobs have no HttpContext to resolve
/// a tenant schema from, so search_path must be pinned explicitly per iteration rather than relying
/// on TenantDbConnectionInterceptor (which only fires from a request).
/// </summary>
public class HseAlertsBackgroundService(IServiceScopeFactory scopeFactory, ILogger<HseAlertsBackgroundService> logger)
    : BackgroundService
{
    private static readonly TimeSpan WarningWindow = TimeSpan.FromDays(30);

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
                    logger.LogError(ex, "HSE alert check failed for schema {Schema} — will retry in 24 hours", schema);
                }
            }
            await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
        }
    }

    private async Task<List<string>> GetTenantSchemasAsync()
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HSEDbContext>();
        return await db.Database
            .SqlQueryRaw<string>("SELECT schema_name FROM information_schema.schemata WHERE schema_name ~ '^tenant_'")
            .ToListAsync();
    }

    private async Task CheckSchemaAsync(string schema)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HSEDbContext>();
        var ticketingClient = scope.ServiceProvider.GetRequiredService<ITicketingServiceClient>();

        await db.Database.OpenConnectionAsync();
#pragma warning disable EF1002 // schema is sourced from information_schema.schemata (not user input) in GetTenantSchemasAsync above — identifiers can't be parameterized via ExecuteSqlAsync anyway.
        await db.Database.ExecuteSqlRawAsync($"SET search_path TO \"{schema}\", public");
#pragma warning restore EF1002

        var cutoff = DateTime.UtcNow.Add(WarningWindow);

        var expiringTraining = await db.HseTrainingRecords
            .Where(t => t.ExpiresOn != null && t.ExpiresOn <= cutoff)
            .ToListAsync();

        foreach (var record in expiringTraining)
        {
            var daysLeft = (int)(record.ExpiresOn!.Value - DateTime.UtcNow).TotalDays;
            var expired = daysLeft <= 0;
            await ticketingClient.CreateAlertAsync(
                tenantSchema: schema,
                source: "HseTraining",
                severity: expired ? "Critical" : "Warning",
                title: expired
                    ? $"HSE training certificate expired — {record.EmployeeName ?? record.EmployeeUserId} ({record.Course})"
                    : $"HSE training certificate expiring in {daysLeft} days — {record.EmployeeName ?? record.EmployeeUserId} ({record.Course})",
                message: expired
                    ? $"{record.EmployeeName ?? record.EmployeeUserId}'s {record.Course} certificate expired on {record.ExpiresOn:d}."
                    : $"{record.EmployeeName ?? record.EmployeeUserId}'s {record.Course} certificate expires on {record.ExpiresOn:d}.",
                assignedToUserId: record.EmployeeUserId,
                requiredPermission: "hse.read");
        }

        var dueInspections = await db.StatutoryInspections
            .Where(i => i.DueDate <= cutoff)
            .ToListAsync();

        foreach (var inspection in dueInspections)
        {
            var daysLeft = (int)(inspection.DueDate - DateTime.UtcNow).TotalDays;
            var overdue = daysLeft <= 0;
            await ticketingClient.CreateAlertAsync(
                tenantSchema: schema,
                source: "HseStatutoryInspection",
                severity: overdue ? "Critical" : "Warning",
                title: overdue
                    ? $"Statutory inspection overdue — {inspection.Equipment} ({inspection.SiteName ?? inspection.SiteId})"
                    : $"Statutory inspection due in {daysLeft} days — {inspection.Equipment} ({inspection.SiteName ?? inspection.SiteId})",
                message: overdue
                    ? $"{inspection.Equipment} at {inspection.SiteName ?? inspection.SiteId} was due for legal inspection on {inspection.DueDate:d}."
                    : $"{inspection.Equipment} at {inspection.SiteName ?? inspection.SiteId} is due for legal inspection on {inspection.DueDate:d}.",
                requiredPermission: "hse.read");
        }

        if (expiringTraining.Count > 0 || dueInspections.Count > 0)
            logger.LogInformation("{TrainingCount} training certs and {InspectionCount} statutory inspections flagged in {Schema}",
                expiringTraining.Count, dueInspections.Count, schema);

        await db.Database.CloseConnectionAsync();
    }
}
