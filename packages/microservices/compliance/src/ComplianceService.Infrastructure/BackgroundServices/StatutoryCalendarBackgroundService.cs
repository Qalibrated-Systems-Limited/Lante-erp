using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ComplianceService.Core.Entities;
using ComplianceService.Core.Enums;
using ComplianceService.Core.Interfaces.Services;
using ComplianceService.Infrastructure.Data;

using ComplianceService.Core.Services;

namespace ComplianceService.Infrastructure.BackgroundServices;

/// <summary>
/// STAT-001: rolls Monthly/Quarterly StatutoryObligations forward into dated StatutoryDeadline
/// rows (Annually obligations — corporate/instalment tax — get manually-created deadlines since
/// their due date varies with the tax calendar). STAT-002/006: 60-day alerts for annual returns
/// and the Tax Compliance Certificate. Runs every 12 hours per tenant schema.
///
/// Alert targeting deliberately uses two different permissions, not one blanket "statutory.read":
/// STAT-002 explicitly asks for annual-return reminders to go to "MD and Company Secretary" only
/// — a narrower audience than the general statutory calendar — so those alerts require
/// "statutory.approve" while routine deadline/TCC reminders use "statutory.read".
/// </summary>
public class StatutoryCalendarBackgroundService(IServiceScopeFactory scopeFactory, ILogger<StatutoryCalendarBackgroundService> logger)
    : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(12);
    private static readonly int[] DeadlineAlertTiers = [90, 60, 30];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(45), stoppingToken); // let migrations finish first

        while (!stoppingToken.IsCancellationRequested)
        {
            foreach (var schema in await GetTenantSchemasAsync())
            {
                try
                {
                    await ProcessSchemaAsync(schema);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Statutory calendar processing failed for schema {Schema} — will retry next cycle", schema);
                }
            }
            await Task.Delay(CheckInterval, stoppingToken);
        }
    }

    private async Task<List<string>> GetTenantSchemasAsync()
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ComplianceDbContext>();
        return await db.Database
            .SqlQueryRaw<string>("SELECT schema_name FROM information_schema.schemata WHERE schema_name ~ '^tenant_'")
            .ToListAsync();
    }

    private async Task ProcessSchemaAsync(string schema)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ComplianceDbContext>();
        var ticketingClient = scope.ServiceProvider.GetRequiredService<ITicketingServiceClient>();

        await db.Database.OpenConnectionAsync();
#pragma warning disable EF1002 // schema is sourced from information_schema.schemata (GetTenantSchemasAsync), not user input — a SQL identifier, which ExecuteSqlAsync parameterization can't target anyway.
        await db.Database.ExecuteSqlRawAsync($"SET search_path TO \"{schema}\", public");
#pragma warning restore EF1002

        await GenerateRecurringDeadlinesAsync(db);
        await FlipOverdueStatusesAsync(db);
        await RaiseDeadlineAlertsAsync(db, ticketingClient, schema);
        await RaiseAnnualReturnAlertsAsync(db, ticketingClient, schema);
        await RaiseTccAlertsAsync(db, ticketingClient, schema);

        await db.Database.CloseConnectionAsync();
    }

    // STAT-001: ensures a StatutoryDeadline row exists for the current period (and one period
    // ahead, so it's visible on the calendar in advance) for every active Monthly/Quarterly
    // obligation. Idempotent — checked by (ObligationId, year, month) before inserting.
    private static async Task GenerateRecurringDeadlinesAsync(ComplianceDbContext db)
    {
        var now = DateTime.UtcNow;
        var activeObligations = await db.StatutoryObligations
            .Where(o => o.IsActive && o.Frequency != ObligationFrequency.Annually)
            .ToListAsync();

        var toCreate = new List<StatutoryDeadline>();
        foreach (var ob in activeObligations)
        {
            // Both the month selection and the day clamping live in StatutoryCalendarRules, which is
            // pure and tested. They were inline here and untestable, and the quarterly selection was
            // wrong at the year boundary — see that class for what and why.
            var candidateMonths = StatutoryCalendarRules.CandidateMonths(ob.Frequency, now);

            foreach (var month in candidateMonths)
            {
                var due = StatutoryCalendarRules.DueDateFor(ob.StatutoryDay, month);
                // Compare via a range rather than d.DueDate.Year/.Month — Npgsql can't translate
                // DateTime member access on a timestamptz column in this provider version (throws
                // "timestamp argument to AtTimeZone had unknown store type TIMESTAMPTZ"), and this
                // never surfaced before because tenant_qsl had zero active Monthly/Quarterly
                // StatutoryObligations rows to exercise the query until now.
                var monthStart = new DateTime(due.Year, due.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                var monthEnd = monthStart.AddMonths(1);
                var exists = await db.StatutoryDeadlines.AnyAsync(d =>
                    d.ObligationId == ob.Id && d.DueDate >= monthStart && d.DueDate < monthEnd);
                if (exists) continue;

                toCreate.Add(new StatutoryDeadline
                {
                    ObligationId = ob.Id,
                    DueDate = due,
                    OwnerUserId = ob.OwnerUserId,
                    OwnerName = ob.OwnerName,
                });
            }
        }

        if (toCreate.Count > 0)
        {
            await db.StatutoryDeadlines.AddRangeAsync(toCreate);
            await db.SaveChangesAsync();
        }
    }

    private static async Task FlipOverdueStatusesAsync(ComplianceDbContext db)
    {
        var now = DateTime.UtcNow;
        var changed = false;

        foreach (var d in await db.StatutoryDeadlines.Where(d => d.Status == StatutoryDeadlineStatus.Pending && d.DueDate < now).ToListAsync())
        {
            d.Status = StatutoryDeadlineStatus.Overdue;
            changed = true;
        }
        foreach (var r in await db.AnnualReturns.Where(r => r.Status == AnnualReturnStatus.Pending && r.DueDate < now).ToListAsync())
        {
            r.Status = AnnualReturnStatus.Overdue;
            changed = true;
        }
        foreach (var t in await db.CosecTasks.Where(t => (t.Status == CosecTaskStatus.Open || t.Status == CosecTaskStatus.InProgress) && t.DueDate < now).ToListAsync())
        {
            t.Status = CosecTaskStatus.Overdue;
            changed = true;
        }

        if (changed) await db.SaveChangesAsync();
    }

    private async Task RaiseDeadlineAlertsAsync(ComplianceDbContext db, ITicketingServiceClient client, string schema)
    {
        var now = DateTime.UtcNow;
        var pending = await db.StatutoryDeadlines
            .Include(d => d.Obligation)
            .Where(d => d.Status != StatutoryDeadlineStatus.Filed && d.DueDate <= now.AddDays(90))
            .ToListAsync();

        foreach (var d in pending)
        {
            var daysLeft = (int)(d.DueDate - now).TotalDays;
            var nearestTier = DeadlineAlertTiers.Where(t => daysLeft <= t).OrderByDescending(t => t).FirstOrDefault(-1);
            if (nearestTier == -1) continue;

            var overdue = daysLeft <= 0;
            var name = d.Obligation?.Name ?? "Statutory obligation";
            await client.CreateAlertAsync(schema, "StatutoryDeadline",
                overdue ? "Critical" : "Warning",
                overdue ? $"{name} filing overdue" : $"{name} due in {daysLeft} days",
                overdue ? $"{name} was due on {d.DueDate:d}." : $"{name} is due on {d.DueDate:d}.",
                requiredPermission: "statutory.read");
        }
    }

    // STAT-002: 60-day alert to MD and Company Secretary specifically — "statutory.approve",
    // narrower than the general "statutory.read" every calendar viewer holds.
    private async Task RaiseAnnualReturnAlertsAsync(ComplianceDbContext db, ITicketingServiceClient client, string schema)
    {
        var now = DateTime.UtcNow;
        var pending = await db.AnnualReturns
            .Where(r => r.Status != AnnualReturnStatus.Filed && r.DueDate <= now.AddDays(60))
            .ToListAsync();

        foreach (var r in pending)
        {
            var daysLeft = (int)(r.DueDate - now).TotalDays;
            var overdue = daysLeft <= 0;
            await client.CreateAlertAsync(schema, "AnnualReturn",
                overdue ? "Critical" : "Warning",
                overdue ? $"Annual return {r.Year} filing overdue" : $"Annual return {r.Year} due in {daysLeft} days",
                overdue ? $"The {r.Year} annual return with the Registrar of Companies was due on {r.DueDate:d}."
                        : $"The {r.Year} annual return with the Registrar of Companies is due on {r.DueDate:d}.",
                requiredPermission: "statutory.approve");
        }
    }

    // STAT-006: 60-day (per-record configurable) alert, auto-reminding Finance to renew via iTax.
    private async Task RaiseTccAlertsAsync(ComplianceDbContext db, ITicketingServiceClient client, string schema)
    {
        var now = DateTime.UtcNow;
        var allTccs = await db.TaxComplianceCerts.ToListAsync();

        foreach (var t in allTccs)
        {
            var daysLeft = (int)(t.ExpiryDate - now).TotalDays;
            if (daysLeft > t.AlertDays) continue;

            var expired = daysLeft <= 0;
            await client.CreateAlertAsync(schema, "TaxComplianceCert",
                expired ? "Critical" : "Warning",
                expired ? "Tax Compliance Certificate expired" : $"Tax Compliance Certificate expiring in {daysLeft} days",
                expired ? $"The TCC expired on {t.ExpiryDate:d} — renew via KRA iTax." : $"The TCC expires on {t.ExpiryDate:d} — renew via KRA iTax.",
                requiredPermission: "statutory.read");
        }
    }
}
