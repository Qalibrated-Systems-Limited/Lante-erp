using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ComplianceService.Core.Interfaces.Services;
using ComplianceService.Infrastructure.Data;

namespace ComplianceService.Infrastructure.BackgroundServices;

/// <summary>
/// COMP-008 (regulatory licence expiry), COMP-009 (anti-bribery training renewal),
/// COMP-004 (data subject request 30-day deadline), COMP-005 (72-hour ODPC breach
/// notification), COMP-002 (January COI declaration reminder), STAT-002 (annual
/// return 60-day MD/Company Secretary alert), and the ICM intercompany transaction
/// 30/45-day reconciliation ageing check — checked per tenant schema, pushed into
/// ticketing's shared Alerts table. Runs every 6 hours rather than the 24-hour
/// cadence HSE/Fleet use for their expiry checks: the 72-hour ODPC notification
/// deadline is legally time-critical enough that a daily poll risks missing it by
/// most of a day.
/// </summary>
public class ComplianceAlertsBackgroundService(IServiceScopeFactory scopeFactory, ILogger<ComplianceAlertsBackgroundService> logger)
    : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(6);
    private static readonly TimeSpan TrainingWarningWindow = TimeSpan.FromDays(30);
    private static readonly TimeSpan DsrWarningWindow = TimeSpan.FromDays(7);
    private static readonly TimeSpan BreachWarningWindow = TimeSpan.FromHours(24);
    private static readonly TimeSpan AnnualReturnWarningWindow = TimeSpan.FromDays(60);
    private static readonly TimeSpan IntercompanyTxnWarningWindow = TimeSpan.FromDays(30);
    private static readonly TimeSpan IntercompanyTxnCriticalWindow = TimeSpan.FromDays(45);

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
                    logger.LogError(ex, "Compliance alert check failed for schema {Schema} — will retry next cycle", schema);
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

    private async Task CheckSchemaAsync(string schema)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ComplianceDbContext>();
        var ticketingClient = scope.ServiceProvider.GetRequiredService<ITicketingServiceClient>();

        await db.Database.OpenConnectionAsync();
#pragma warning disable EF1002 // schema is sourced from information_schema.schemata (GetTenantSchemasAsync), not user input — a SQL identifier, which ExecuteSqlAsync parameterization can't target anyway.
        await db.Database.ExecuteSqlRawAsync($"SET search_path TO \"{schema}\", public");
#pragma warning restore EF1002

        var now = DateTime.UtcNow;

        // COMP-008 / STAT-003,004,005,007: regulatory licence expiry. Each licence carries its own
        // AlertDays lead time (STAT-003 NCA / STAT-004 NEMA: 90 days); KEBS/NMK (STAT-005) always
        // additionally gets a fixed second reminder at 30 days regardless of AlertDays.
        var allLicences = await db.RegulatoryLicences.ToListAsync();
        var licenceAlertsRaised = 0;
        foreach (var l in allLicences)
        {
            var daysLeft = (int)(l.ExpiryDate - now).TotalDays;
            var dueTiers = new List<int> { l.AlertDays };
            if (l.Type == Core.Enums.LicenceType.KebsNmk && !dueTiers.Contains(30))
                dueTiers.Add(30);

            // Fire only once we've crossed into the nearest due tier (or are already overdue) —
            // not every check between "90 days out" and expiry, else it'd re-fire every 6 hours
            // regardless (harmless since ticketing dedupes by title, but keeps titles/log clean).
            var nearestTier = dueTiers.Where(t => daysLeft <= t).OrderByDescending(t => t).FirstOrDefault(-1);
            if (nearestTier == -1) continue;

            var expired = daysLeft <= 0;
            licenceAlertsRaised++;
            await ticketingClient.CreateAlertAsync(schema, "RegulatoryLicence",
                expired ? "Critical" : "Warning",
                expired ? $"Regulatory licence expired — {l.Authority}" : $"Regulatory licence expiring in {daysLeft} days — {l.Authority}",
                expired ? $"{l.Authority} licence {(l.LicenceNumber != null ? $"({l.LicenceNumber}) " : "")}expired on {l.ExpiryDate:d}."
                        : $"{l.Authority} licence {(l.LicenceNumber != null ? $"({l.LicenceNumber}) " : "")}expires on {l.ExpiryDate:d}.",
                requiredPermission: "compliance.read");
        }

        // COMP-009: anti-bribery training renewal (2-year cycle)
        var expiringTraining = await db.AntiBriberyTrainings.Where(t => t.NextDueOn <= now.Add(TrainingWarningWindow)).ToListAsync();
        foreach (var t in expiringTraining)
        {
            var daysLeft = (int)(t.NextDueOn - now).TotalDays;
            var overdue = daysLeft <= 0;
            await ticketingClient.CreateAlertAsync(schema, "AntiBriberyTraining",
                overdue ? "Critical" : "Warning",
                overdue ? $"Anti-bribery training renewal overdue — {t.EmployeeName ?? t.EmployeeUserId}" : $"Anti-bribery training renewal due in {daysLeft} days — {t.EmployeeName ?? t.EmployeeUserId}",
                overdue ? $"{t.EmployeeName ?? t.EmployeeUserId}'s anti-bribery training renewal was due on {t.NextDueOn:d}."
                        : $"{t.EmployeeName ?? t.EmployeeUserId}'s anti-bribery training renewal is due on {t.NextDueOn:d}.",
                requiredPermission: "compliance.read",
                assignedToUserId: t.EmployeeUserId);
        }

        // COMP-004: data subject request 30-day statutory deadline
        var dueDsrs = await db.DataSubjectRequests
            .Where(d => d.Status != Core.Enums.DsrStatus.Completed && d.DueBy <= now.Add(DsrWarningWindow))
            .ToListAsync();
        foreach (var d in dueDsrs)
        {
            var daysLeft = (int)(d.DueBy - now).TotalDays;
            var overdue = daysLeft <= 0;
            await ticketingClient.CreateAlertAsync(schema, "DataSubjectRequest",
                overdue ? "Critical" : "Warning",
                overdue ? $"Data subject request overdue (30-day DPA deadline) — {d.RequestorName}" : $"Data subject request due in {daysLeft} days — {d.RequestorName}",
                overdue ? $"{d.Type} request from {d.RequestorName} was due on {d.DueBy:d} (DPA 2019 30-day limit)."
                        : $"{d.Type} request from {d.RequestorName} is due on {d.DueBy:d} (DPA 2019 30-day limit).",
                requiredPermission: "compliance.read");
        }

        // COMP-005: 72-hour ODPC breach notification
        var pendingBreaches = await db.DataBreaches
            .Where(b => b.OdpcNotifiedAt == null && b.OdpcNotificationDueAt <= now.Add(BreachWarningWindow))
            .ToListAsync();
        foreach (var b in pendingBreaches)
        {
            var hoursLeft = (int)(b.OdpcNotificationDueAt - now).TotalHours;
            var overdue = hoursLeft <= 0;
            await ticketingClient.CreateAlertAsync(schema, "DataBreach",
                "Critical",
                overdue ? "ODPC 72-hour breach notification deadline PASSED" : $"ODPC 72-hour breach notification due in {hoursLeft} hours",
                overdue ? $"Data breach discovered {b.DiscoveredAt:g} — the 72-hour ODPC notification deadline ({b.OdpcNotificationDueAt:g}) has passed and no notification is recorded."
                        : $"Data breach discovered {b.DiscoveredAt:g} — ODPC must be notified by {b.OdpcNotificationDueAt:g}.",
                requiredPermission: "compliance.read");
        }

        // COMP-002: January COI declaration reminder workflow
        if (now.Month == 1)
        {
            await ticketingClient.CreateAlertAsync(schema, "CoiDeclaration", "Warning",
                $"Annual conflict-of-interest declarations due — {now.Year}",
                $"Department Heads and above must submit their {now.Year} conflict-of-interest declaration this month.",
                requiredPermission: "compliance.read");
        }

        // STAT-002: annual return 60-day alert, routed to the narrower statutory.approve
        // (MD/Company Secretary) tier rather than the general statutory.read audience.
        var dueAnnualReturns = await db.AnnualReturns
            .Where(a => a.Status != Core.Enums.AnnualReturnStatus.Filed && a.DueDate <= now.Add(AnnualReturnWarningWindow))
            .ToListAsync();
        foreach (var a in dueAnnualReturns)
        {
            var daysLeft = (int)(a.DueDate - now).TotalDays;
            var overdue = daysLeft <= 0;
            await ticketingClient.CreateAlertAsync(schema, "AnnualReturn",
                overdue ? "Critical" : "Warning",
                overdue ? $"Annual return overdue — {a.Year}" : $"Annual return due in {daysLeft} days — {a.Year}",
                overdue ? $"The {a.Year} annual return with the Registrar of Companies was due on {a.DueDate:d} and has not been filed."
                        : $"The {a.Year} annual return with the Registrar of Companies is due on {a.DueDate:d}.",
                requiredPermission: "statutory.approve");
        }

        // ICM: dual-ledger intercompany transaction reconciliation age (30-day warning,
        // 45-day critical), per the ERD companion doc's Inter-Company design notes.
        var unreconciledTxns = await db.IntercompanyTxns.Where(t => t.ReconciledAt == null).ToListAsync();
        var intercompanyAlertsRaised = 0;
        foreach (var t in unreconciledTxns)
        {
            var ageDays = (int)(now - t.PostedAt).TotalDays;
            if (ageDays < (int)IntercompanyTxnWarningWindow.TotalDays) continue;
            intercompanyAlertsRaised++;
            var critical = ageDays >= (int)IntercompanyTxnCriticalWindow.TotalDays;
            await ticketingClient.CreateAlertAsync(schema, "IntercompanyTxn",
                critical ? "Critical" : "Warning",
                $"Intercompany transaction unreconciled for {ageDays} days — {t.QslLedgerRef}",
                $"Intercompany recharge {t.QslLedgerRef}/{t.SisterLedgerRef} posted on {t.PostedAt:d} has not been reconciled between both ledgers after {ageDays} days.",
                requiredPermission: "compliance.read");
        }

        if (licenceAlertsRaised + expiringTraining.Count + dueDsrs.Count + pendingBreaches.Count + dueAnnualReturns.Count + intercompanyAlertsRaised > 0)
            logger.LogInformation(
                "Compliance alerts in {Schema}: {Licences} licences, {Training} training, {Dsr} DSRs, {Breaches} breaches, {AnnualReturns} annual returns, {Intercompany} intercompany",
                schema, licenceAlertsRaised, expiringTraining.Count, dueDsrs.Count, pendingBreaches.Count, dueAnnualReturns.Count, intercompanyAlertsRaised);

        await db.Database.CloseConnectionAsync();
    }
}
