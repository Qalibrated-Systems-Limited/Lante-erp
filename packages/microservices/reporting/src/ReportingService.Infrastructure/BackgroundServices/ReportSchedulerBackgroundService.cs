using Cronos;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ReportingService.Core.Entities;
using ReportingService.Core.Enums;
using ReportingService.Core.Interfaces.Services;
using ReportingService.Infrastructure.Data;
using ReportingService.Infrastructure.Services;

namespace ReportingService.Infrastructure.BackgroundServices;

/// <summary>
/// RPT-002/003/005: polls each tenant schema for due ReportSchedules, generates the report via
/// ReportGenerationService (the same dispatch the "run now" controller action uses), writes a
/// ReportRun, and emails active ReportRecipients through ticketing's internal/email endpoint.
/// Runs every 15 minutes — finer than Compliance's 6-hour alert cadence since report schedules
/// can reasonably ask for hour-level delivery windows.
/// </summary>
public class ReportSchedulerBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<ReportSchedulerBackgroundService> logger,
    IConfiguration config)
    : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); // let migrations finish first

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
                    logger.LogError(ex, "Report scheduler failed for schema {Schema} — will retry next cycle", schema);
                }
            }
            await Task.Delay(CheckInterval, stoppingToken);
        }
    }

    private async Task<List<string>> GetTenantSchemasAsync()
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ReportingDbContext>();
        return await db.Database
            .SqlQueryRaw<string>("SELECT schema_name FROM information_schema.schemata WHERE schema_name ~ '^tenant_'")
            .ToListAsync();
    }

    private async Task ProcessSchemaAsync(string schema)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ReportingDbContext>();

        await db.Database.OpenConnectionAsync();
#pragma warning disable EF1002 // schema is sourced from information_schema.schemata (GetTenantSchemasAsync), not user input, and identifiers can't be parameterized via ExecuteSqlAsync anyway.
        await db.Database.ExecuteSqlRawAsync($"SET search_path TO \"{schema}\", public");
#pragma warning restore EF1002

        var now = DateTime.UtcNow;
        var dueSchedules = await db.ReportSchedules
            .Where(s => s.IsActive && (s.NextRunAt == null || s.NextRunAt <= now))
            .ToListAsync();

        var processed = 0;
        var skipped = 0;
        foreach (var schedule in dueSchedules)
        {
            var definition = await db.ReportDefinitions.FirstOrDefaultAsync(d => d.Id == schedule.ReportDefinitionId);
            if (definition == null || !definition.IsActive) continue;

            // CLAIM before running. The read above and the run below are separated by report generation
            // and upstream HTTP calls, so at two replicas both timers see the same schedule as due and
            // both generate it — and both EMAIL it. The claim is what makes exactly one win (#219).
            if (!await TryClaimAsync(db, schedule, now))
            {
                skipped++;
                continue;
            }

            await RunScheduleAsync(schema, schedule, definition);
            processed++;
        }

        if (skipped > 0)
            logger.LogDebug("Report scheduler skipped {Count} schedule(s) in {Schema} already claimed by another instance",
                skipped, schema);

        if (processed > 0)
            logger.LogInformation("Report scheduler processed {Count} due schedule(s) in {Schema}", processed, schema);

        await db.Database.CloseConnectionAsync();
    }

    /// <summary>
    /// The claim, as a single conditional UPDATE. Exposed <c>internal</c> so the tests execute THIS
    /// statement rather than a copy of it — a test holding its own transcription of the SQL proves the
    /// test is sensitive to the semantics, not that the production statement has them.
    ///
    /// <para>Parameters: {0} now, {1} next run, {2} schedule id, {3} now again for the due-ness test.</para>
    /// </summary>
    internal const string ClaimSql =
        """
        UPDATE "ReportSchedules"
        SET "LastRunAt" = {0}, "NextRunAt" = {1}
        WHERE "Id" = {2}
          AND "IsActive"
          AND NOT "IsDeleted"
          AND ("NextRunAt" IS NULL OR "NextRunAt" <= {3})
        """;

    /// <summary>
    /// Claims a due schedule for this instance by advancing <c>NextRunAt</c> in a single conditional
    /// UPDATE. Returns false when another instance got there first.
    ///
    /// <para><b>Why an atomic claim rather than the advisory lock #219 suggests.</b> The codebase's
    /// existing <c>pg_advisory_xact_lock</c> uses — <c>DepreciationService</c> and
    /// <c>SopLibraryService</c> — both wrap a SHORT critical section inside one transaction. Here the
    /// critical section is a report generation plus HTTP calls to other services plus an email; holding
    /// a transaction open across that would be a minutes-long transaction pinning a connection, and a
    /// crashed pod would hold the lock until its session died. A conditional UPDATE moves the mutual
    /// exclusion into the database, needs no transaction spanning the work, and cannot be orphaned.</para>
    ///
    /// <para>The WHERE clause repeats the due-ness test rather than trusting the row read moments ago:
    /// re-checking inside the atomic statement is the whole point, since the other instance may have
    /// advanced it in between.</para>
    ///
    /// <para>Claiming BEFORE the run — rather than marking it after — deliberately preserves existing
    /// behaviour: the previous code advanced <c>NextRunAt</c> whether the run succeeded or failed, so a
    /// failure has always waited for the next cron occurrence rather than retrying immediately.</para>
    /// </summary>
    private async Task<bool> TryClaimAsync(ReportingDbContext db, ReportSchedule schedule, DateTime now)
    {
        var next = ComputeNextRunAt(schedule.CronExpression);

        // A cron with no future occurrence (e.g. 30 February) yields null. NextRunAt is OVERLOADED — the
        // due query treats null as "run now", which is right for a schedule that has never run and wrong
        // for one that can never run again. Writing null back would leave the row permanently due: it
        // would run on EVERY tick forever, and the claim below could not exclude a second instance
        // because the row would still match its own WHERE clause.
        //
        // So a schedule whose cron has no future occurrence is deactivated instead, loudly. That fixes a
        // pre-existing rerun loop as well as making the claim sound, and it is the honest reading — a
        // schedule that can never fire again is not active.
        if (next is null)
        {
            logger.LogError(
                "Report schedule {ScheduleId} has cron '{Cron}' with no future occurrence — deactivating. "
                + "It would otherwise be treated as due on every tick.",
                schedule.Id, schedule.CronExpression);

            await db.Database.ExecuteSqlRawAsync(
                """UPDATE "ReportSchedules" SET "IsActive" = FALSE, "LastRunAt" = {0} WHERE "Id" = {1}""",
                now, schedule.Id);
            schedule.IsActive = false;
            return false;
        }

        var claimed = await db.Database.ExecuteSqlRawAsync(ClaimSql, now, next.Value, schedule.Id, now);

        if (claimed == 0) return false;

        // Keep the tracked entity consistent with what was just written, so RunScheduleAsync does not
        // see stale values and no longer needs to write them itself.
        schedule.LastRunAt = now;
        schedule.NextRunAt = next;
        return true;
    }

    private async Task RunScheduleAsync(string schema, ReportSchedule schedule, ReportDefinition definition)
    {
        using var runScope = scopeFactory.CreateScope();
        var sp = runScope.ServiceProvider;
        var db = sp.GetRequiredService<ReportingDbContext>();
        await db.Database.OpenConnectionAsync();
#pragma warning disable EF1002 // schema is sourced from information_schema.schemata (GetTenantSchemasAsync), not user input, and identifiers can't be parameterized via ExecuteSqlAsync anyway.
        await db.Database.ExecuteSqlRawAsync($"SET search_path TO \"{schema}\", public");
#pragma warning restore EF1002

        var run = new ReportRun
        {
            ReportDefinitionId = definition.Id,
            ReportScheduleId = schedule.Id,
            Format = schedule.Format,
            TriggeredBy = "scheduler",
        };

        // Impersonate a system identity for the upstream calls this report needs — a scheduled
        // run has no real user to forward a JWT for (see SystemTokenIssuer).
        var tokenIssuer = sp.GetRequiredService<SystemTokenIssuer>();
        var token = await tokenIssuer.IssueForAsync(schema);
        var httpContextAccessor = sp.GetRequiredService<IHttpContextAccessor>();
        var fakeContext = new DefaultHttpContext();
        fakeContext.Request.Headers.Authorization = $"Bearer {token}";
        httpContextAccessor.HttpContext = fakeContext;

        var generator = sp.GetRequiredService<ReportGenerationService>();
        var result = await generator.GenerateAsync(sp, schema, definition.Key, run.Id, schedule.Format);

        httpContextAccessor.HttpContext = null;

        if (result.Success)
        {
            run.FileUrl = result.FileUrl;
            run.Status = RunStatus.Success;
        }
        else
        {
            logger.LogError("Failed generating scheduled report {Key} for schema {Schema}: {Error}", definition.Key, schema, result.ErrorMessage);
            run.Status = RunStatus.Failed;
            run.ErrorMessage = result.ErrorMessage;
        }

        db.ReportRuns.Add(run);

        // LastRunAt/NextRunAt were written by TryClaimAsync, which is what serialised this run. Writing
        // them again here would recompute NextRunAt from a later "now" and quietly drift the schedule
        // forward by the report's own duration on every run.
        await db.SaveChangesAsync();

        if (run.Status == RunStatus.Success)
            await DeliverAsync(sp, schema, schedule, definition, run);

        await db.Database.CloseConnectionAsync();
    }

    private static DateTime? ComputeNextRunAt(string cronExpression)
    {
        var cron = CronExpression.Parse(cronExpression);
        return cron.GetNextOccurrence(DateTime.UtcNow, TimeZoneInfo.Utc);
    }

    private async Task DeliverAsync(IServiceProvider sp, string schema, ReportSchedule schedule, ReportDefinition definition, ReportRun run)
    {
        var db = sp.GetRequiredService<ReportingDbContext>();
        var recipients = await db.ReportRecipients
            .Where(r => r.ReportScheduleId == schedule.Id && r.IsActive)
            .ToListAsync();
        if (recipients.Count == 0) return;

        var publicBaseUrl = (config["Storage:BaseUrl"] ?? "http://lante-reporting-service:8080").TrimEnd('/');
        var downloadUrl = $"{publicBaseUrl}{run.FileUrl}";

        var ticketingClient = sp.GetRequiredService<ITicketingServiceClient>();
        await ticketingClient.SendReportEmailAsync(
            schema,
            recipients.Select(r => r.Email).ToArray(),
            $"Scheduled report: {definition.Name}",
            $"<p>Your scheduled report \"{definition.Name}\" generated on {run.GeneratedAt:g} is ready.</p>" +
            $"<p><a href=\"{downloadUrl}\">Download {definition.Name}</a></p>");
    }
}
