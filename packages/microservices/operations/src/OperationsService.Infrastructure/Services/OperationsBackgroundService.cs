using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OperationsService.Core.Entities;
using OperationsService.Core.Enums;
using OperationsService.Infrastructure.Data;

namespace OperationsService.Infrastructure.Services;

/// <summary>
/// Per-tenant-schema background sweep for operations — mirrors the ticketing SLABackgroundService.
/// Background jobs have no HTTP request to resolve a tenant schema from, so this discovers every
/// provisioned tenant_* schema directly from Postgres and runs each check pinned to that schema's
/// search_path (otherwise the interceptor would fall back to "public" and only ever see unmigrated
/// tenants). SCAFFOLD: the individual check methods are placeholders that later phases fill in.
/// </summary>
public class OperationsBackgroundService(
    IServiceScopeFactory scopeFactory,
    IConfiguration config,
    ILogger<OperationsBackgroundService> logger)
    : BackgroundService
{
    private static readonly TimeSpan SweepInterval = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Operations background service started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunChecksAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Operations background sweep failed.");
            }
            await Task.Delay(SweepInterval, stoppingToken);
        }

        logger.LogInformation("Operations background service stopped.");
    }

    private async Task RunChecksAsync(CancellationToken ct)
    {
        foreach (var schema in await GetTenantSchemasAsync())
        {
            try
            {
                await RunChecksForSchemaAsync(schema, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Operations checks failed for schema {Schema}", schema);
            }
        }
    }

    private async Task<List<string>> GetTenantSchemasAsync()
    {
        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OperationsDbContext>();
        return await context.Database
            .SqlQueryRaw<string>("SELECT schema_name FROM information_schema.schemata WHERE schema_name ~ '^tenant_'")
            .ToListAsync();
    }

    private async Task RunChecksForSchemaAsync(string schema, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OperationsDbContext>();

        // Pin this scope's connection to the tenant schema and keep it open for the whole method —
        // if it closed/reopened partway the interceptor would silently reset search_path to "public".
        await context.Database.OpenConnectionAsync(ct);
        try
        {
#pragma warning disable EF1002 // schema is read from information_schema.schemata (GetTenantSchemasAsync, filtered to '^tenant_') — not user input, and identifiers can't be parameterized via ExecuteSqlAsync anyway.
            await context.Database.ExecuteSqlRawAsync($"SET search_path TO \"{schema}\", public", ct);
#pragma warning restore EF1002

            // ── Alert engines (filled in by later phases) ─────────────────────────────
            await CheckBudgetBurnAsync(schema, context, ct);              // O2 — 80/90/95/100% burn alerts
            await CheckMilestoneBreachesAsync(schema, context, ct);       // O3 — 5PM daily milestone-update breach
            await CheckFieldServiceReportOverdueAsync(schema, context, ct); // O5/O7 — FSR not submitted within 24h
            await CheckCalibrationStandardExpiryAsync(schema, context, ct); // O6 — reference-standard 60/30-day expiry
            await CheckCertificateRecallAsync(schema, context,             // O6.1 — client certificate 60/30/7-day recall
                scope.ServiceProvider.GetRequiredService<Core.Interfaces.Services.ICrmCustomerDirectory>(), ct);
            await CheckNegligenceDeadlinesAsync(schema, context, ct);     // O9 — negligence 24h log / 5-day response
            await GenerateRecurringProjectsAsync(schema, scope.ServiceProvider, ct); // PR4b — raise due recurring projects
        }
        finally
        {
            await context.Database.CloseConnectionAsync();
        }
    }

    // ── PR4b — recurring project generation ──────────────────────────────────────────
    // Raises the next occurrence of any standing schedule whose lead-time window has opened. The
    // service itself rolls the schedule forward and skips missed cycles, so a sweep that ran late (or
    // not at all for a while) still lands on the next real date rather than firing repeatedly.
    private async Task GenerateRecurringProjectsAsync(string schema, IServiceProvider sp, CancellationToken ct)
    {
        try
        {
            var recurring = sp.GetRequiredService<Core.Interfaces.Services.IRecurringProjectService>();
            var created = await recurring.GenerateDueAsync("system", ct);
            if (created > 0)
                logger.LogInformation("Raised {Count} recurring project(s) for schema {Schema}.", created, schema);
        }
        catch (Exception ex)
        {
            // One tenant's bad schedule must not stop the rest of the sweep for this schema.
            logger.LogError(ex, "Recurring project generation failed for schema {Schema}", schema);
        }
    }

    // ── O2 — budget-burn alert engine ────────────────────────────────────────────────
    // For each active project with a budget, compute burn = (spent + committed) / planned. Cross the
    // 80/90/95/100% thresholds exactly once each (tracked by Project.AlertNNSentAt), writing a
    // PROJECT_ALERT_LOG row per crossing. 100% also sets the hard budget lock.
    private async Task CheckBudgetBurnAsync(string schema, OperationsDbContext db, CancellationToken ct)
    {
        var projects = await db.Projects
            .Where(p => p.Status == ProjectStatus.Active && p.PlannedBudget > 0m)
            .ToListAsync(ct);

        var now = DateTime.UtcNow;
        var dirty = false;

        foreach (var p in projects)
        {
            var exposure = p.ActualCost + p.Committed;
            var burnPct  = Math.Round(exposure / p.PlannedBudget * 100m, 2);

            void Raise(int threshold)
            {
                db.ProjectAlertLogs.Add(new ProjectAlertLog
                {
                    ProjectId       = p.Id,
                    ThresholdPct    = threshold,
                    SpentAmount     = p.ActualCost,
                    CommittedAmount = p.Committed,
                    BudgetAmount    = p.PlannedBudget,
                    BurnPct         = burnPct,
                    Message         = $"Project '{p.Name}' budget burn reached {burnPct:0.##}% (crossed the {threshold}% threshold).",
                    CreatedBy       = "system",
                    UpdatedBy       = "system",
                });
                dirty = true;
                logger.LogWarning("Budget alert [{Schema}] project {ProjectId} at {Burn}% (>= {Threshold}%)",
                    schema, p.Id, burnPct, threshold);
            }

            if (burnPct >= 80m  && p.Alert80SentAt  == null) { Raise(80);  p.Alert80SentAt  = now; }
            if (burnPct >= 90m  && p.Alert90SentAt  == null) { Raise(90);  p.Alert90SentAt  = now; }
            if (burnPct >= 95m  && p.Alert95SentAt  == null) { Raise(95);  p.Alert95SentAt  = now; }
            if (burnPct >= 100m && p.Alert100SentAt == null) { Raise(100); p.Alert100SentAt = now; p.BudgetLocked = true; }
        }

        if (dirty) await db.SaveChangesAsync(ct);
    }

    // ── O3 — daily 5PM milestone-update breach + liquidated-damages accrual ────────────
    // After the daily deadline hour, any in-progress milestone on an active project with no update
    // logged today is flagged with a breach row (once per day, PM + DeptHead named). Separately, any
    // overdue LdApplies milestone accrues LD = min(cap, value × rate/day × days late).
    // (Deadline hour is UTC; per-branch local hours are deferred until branch timezones land.)
    private async Task CheckMilestoneBreachesAsync(string schema, OperationsDbContext db, CancellationToken ct)
    {
        var now          = DateTime.UtcNow;
        var today        = now.Date;
        var deadlineHour = config.GetValue("Milestones:DailyUpdateDeadlineHour", 17);

        var milestones = await db.Milestones
            .Include(m => m.Project)
            .Where(m => m.Status != MilestoneStatus.Completed
                     && m.SignOffAt == null
                     && m.Project.Status == ProjectStatus.Active)
            .ToListAsync(ct);

        var dirty = false;

        foreach (var m in milestones)
        {
            // 5PM update breach (in-progress milestones only; NotStarted work hasn't begun)
            var noUpdateToday = m.LastUpdatedAt == null || m.LastUpdatedAt.Value.Date < today;
            var notAlertedToday = m.BreachAlertedOn == null || m.BreachAlertedOn.Value.Date < today;
            if (m.Status == MilestoneStatus.InProgress && now.Hour >= deadlineHour && noUpdateToday && notAlertedToday)
            {
                db.MilestoneUpdateLogs.Add(new MilestoneUpdateLog
                {
                    MilestoneId     = m.Id,
                    ProjectId       = m.ProjectId,
                    Note            = $"No progress update logged by {deadlineHour:00}:00 — escalated to PM ({m.Project.ProjectManagerId}) and department head ({m.Project.DepartmentId}).",
                    StatusAtUpdate  = m.Status.ToString(),
                    ProgressPct     = m.ProgressPct,
                    IsBreachAlert   = true,
                    UpdatedByUserId = "system",
                    CreatedBy       = "system",
                    UpdatedBy       = "system",
                });
                m.BreachAlertedOn = today;
                dirty = true;
                logger.LogWarning("Milestone breach [{Schema}] milestone {Id} project {ProjectId} — no update by {Hour}:00",
                    schema, m.Id, m.ProjectId, deadlineHour);
            }

            // Liquidated-damages accrual for overdue milestones
            if (m.LdApplies && m.Project.LdRatePerDay > 0m && m.DueDate.Date < today)
            {
                var baseAmt   = m.PlannedAmount ?? 0m;
                var delayDays = (today - m.DueDate.Date).Days;
                var cap       = baseAmt * m.Project.LdCapPct;
                var accrued   = Math.Min(cap, Math.Round(baseAmt * m.Project.LdRatePerDay * delayDays, 2));
                if (accrued != m.LdAmount)
                {
                    m.LdAmount = accrued;
                    dirty = true;
                    logger.LogInformation("LD accrual [{Schema}] milestone {Id} = {Ld:0.00} ({Days}d late)",
                        schema, m.Id, accrued, delayDays);
                }
            }
        }

        if (dirty) await db.SaveChangesAsync(ct);
    }
    // ── O5-FSR — Field Service Report not submitted within the SLA window (default 24h) ───────────
    // A completed assignment whose FSR hasn't been submitted within Fsr:OverdueHours of completion is
    // flagged once (Assignment.FsrOverdueAlertedAt). Submission = a non-deleted report with SubmittedAt.
    private async Task CheckFieldServiceReportOverdueAsync(string schema, OperationsDbContext db, CancellationToken ct)
    {
        var overdueHours = config.GetValue("Fsr:OverdueHours", 24);
        var cutoff = DateTime.UtcNow.AddHours(-overdueHours);

        var candidates = await db.Assignments
            .Include(a => a.ServiceReports)
            .Where(a => a.Status == AssignmentStatus.Completed
                     && a.CompletedAt != null
                     && a.CompletedAt < cutoff
                     && a.FsrOverdueAlertedAt == null)
            .ToListAsync(ct);

        var dirty = false;
        foreach (var a in candidates)
        {
            if (a.ServiceReports.Any(r => !r.IsDeleted && r.SubmittedAt != null)) continue;  // FSR delivered

            a.FsrOverdueAlertedAt = DateTime.UtcNow;
            dirty = true;
            logger.LogWarning("FSR overdue [{Schema}] assignment {Id} ('{Title}') — completed {Completed:u}, no report submitted within {Hours}h",
                schema, a.Id, a.Title, a.CompletedAt, overdueHours);
        }

        if (dirty) await db.SaveChangesAsync(ct);
    }
    // ── O6 — reference-standard calibration expiry (60/30-day advance alerts) ─────────
    // An active standard whose own calibration falls due within 60 / 30 days is alerted once per
    // threshold (guarded by Alert60/30SentAt). Renewing the NextDueDate reopens the alerts.
    private async Task CheckCalibrationStandardExpiryAsync(string schema, OperationsDbContext db, CancellationToken ct)
    {
        var today = DateTime.UtcNow.Date;

        var standards = await db.ReferenceStandards
            .Where(s => s.Status == ReferenceStandardStatus.Active && s.NextDueDate != null)
            .ToListAsync(ct);

        var now = DateTime.UtcNow;
        var dirty = false;

        foreach (var s in standards)
        {
            var daysLeft = (s.NextDueDate!.Value.Date - today).Days;

            if (daysLeft <= 60 && s.Alert60SentAt == null)
            {
                s.Alert60SentAt = now;
                dirty = true;
                logger.LogWarning("Reference-standard expiry [{Schema}] {Asset} due in {Days}d ({Due:yyyy-MM-dd}) — <=60d",
                    schema, s.AssetId, daysLeft, s.NextDueDate);
            }
            if (daysLeft <= 30 && s.Alert30SentAt == null)
            {
                s.Alert30SentAt = now;
                dirty = true;
                logger.LogWarning("Reference-standard expiry [{Schema}] {Asset} due in {Days}d ({Due:yyyy-MM-dd}) — <=30d",
                    schema, s.AssetId, daysLeft, s.NextDueDate);
            }
        }

        if (dirty) await db.SaveChangesAsync(ct);
    }
    // ── O6.1 — issued-certificate client recall ──────────────────────────────────────
    // Certificates approaching their next-calibration date are reported to CRM, which owns client
    // contact and sends the recall. Three tiers (60/30/7 days), each fired at most once and recorded
    // on the certificate so the register can show — and an auditor can see — that the client was
    // chased. Withdrawn certificates are skipped: they are no longer in force.
    private async Task CheckCertificateRecallAsync(
        string schema, OperationsDbContext db, Core.Interfaces.Services.ICrmCustomerDirectory crm, CancellationToken ct)
    {
        var today = DateTime.UtcNow.Date;
        var now   = DateTime.UtcNow;

        // The widest tier bounds the query, so a lab with years of history only loads the few
        // certificates actually approaching recall.
        var horizon = today.AddDays(60);

        var due = await db.CalibrationCertificates
            .Where(c => c.WithdrawnAt == null
                     && c.NextCalibrationDue != null
                     && c.NextCalibrationDue <= horizon
                     && (c.Recall60SentAt == null || c.Recall30SentAt == null || c.Recall7SentAt == null))
            .ToListAsync(ct);

        if (due.Count == 0) return;

        // Fallback contact: the service request that produced the certificate is the only place
        // operations itself holds a client email.
        var srIds = due.Where(c => c.ServiceRequestId != null).Select(c => c.ServiceRequestId!).Distinct().ToList();
        var srs = new Dictionary<string, (string? Email, string? CrmId)>();
        if (srIds.Count > 0)
        {
            var loaded = await db.ServiceRequests
                .Where(s => srIds.Contains(s.Id))
                .Select(s => new { s.Id, s.ClientEmail, s.CrmCustomerId })
                .ToListAsync(ct);
            foreach (var s in loaded)
                srs[s.Id] = (Email: s.ClientEmail, CrmId: s.CrmCustomerId);
        }

        var dirty = false;

        foreach (var c in due)
        {
            var daysLeft = (c.NextCalibrationDue!.Value.Date - today).Days;

            // Which tiers have been crossed but not yet sent. A certificate that sat unswept until
            // it was nearly due crosses several at once — mark them all, but send one notice, at the
            // most urgent tier. Three simultaneous emails would read as a system fault to the client.
            var crossed = new List<int>();
            if (daysLeft <= 60 && c.Recall60SentAt == null) crossed.Add(60);
            if (daysLeft <= 30 && c.Recall30SentAt == null) crossed.Add(30);
            if (daysLeft <= 7  && c.Recall7SentAt  == null) crossed.Add(7);
            if (crossed.Count == 0) continue;

            var tier = crossed.Min();

            var sr = c.ServiceRequestId != null && srs.TryGetValue(c.ServiceRequestId, out var s) ? s : default;

            // Address resolution, most reliable first:
            //   1. the CRM customer id anchored on the certificate — exact, and reflects address
            //      changes made since issue;
            //   2. the id on the originating service request, which covers certificates issued
            //      before the anchor existed but whose request has since been anchored;
            //   3. the address captured on the request itself;
            //   4. an unambiguous CRM match on client name, for the oldest records. Ambiguous name
            //      matches resolve to null rather than risk mailing the wrong client.
            var crmId = !string.IsNullOrWhiteSpace(c.CrmCustomerId) ? c.CrmCustomerId : sr.CrmId;

            string? email = null;
            if (!string.IsNullOrWhiteSpace(crmId))
                email = await crm.GetCustomerEmailByIdAsync(crmId!, schema, ct);
            if (string.IsNullOrWhiteSpace(email) && !string.IsNullOrWhiteSpace(sr.Email))
                email = sr.Email;
            if (string.IsNullOrWhiteSpace(email) && !string.IsNullOrWhiteSpace(c.ClientName))
                email = await crm.GetCustomerEmailAsync(c.ClientName!, schema, ct);

            try
            {
                // The schema is passed explicitly: this runs outside any HTTP request, so a seam
                // that resolved it from HttpContext would find none and silently skip the send.
                await crm.ReportCalibrationDueAsync(
                    new Core.Interfaces.Services.CalibrationDueNotice(
                        c.Number, c.ClientName, email, c.NextCalibrationDue.Value),
                    schema, ct);
            }
            catch (Exception ex)
            {
                // Leave the tier unmarked so the next sweep retries rather than silently dropping
                // this client's recall.
                logger.LogWarning(ex, "Certificate recall [{Schema}] {Cert} tier {Tier}d failed — will retry next sweep.",
                    schema, c.Number, tier);
                continue;
            }

            foreach (var t in crossed)
            {
                if (t == 60) c.Recall60SentAt = now;
                if (t == 30) c.Recall30SentAt = now;
                if (t == 7)  c.Recall7SentAt  = now;
            }
            dirty = true;

            logger.LogInformation(
                "Certificate recall [{Schema}] {Cert} for {Client} due {Due:yyyy-MM-dd} ({Days}d) — {Tier}d tier reported to CRM{Addr}.",
                schema, c.Number, c.ClientName ?? "n/a", c.NextCalibrationDue, daysLeft, tier,
                string.IsNullOrWhiteSpace(email) ? " (no client address resolved)" : $" for {email}");
        }

        if (dirty) await db.SaveChangesAsync(ct);
    }

    // ── O9 — negligence response deadlines + program overrun notices ─────────────────
    // (1) An open negligence incident past its 5-day response deadline escalates to the MD (once).
    // (2) An active project more than 14 days past its expected end date (not yet completed) gets a
    //     PROGRAM_OVERRUN_NOTICE (once, guarded by Project.OverrunNoticedAt).
    private async Task CheckNegligenceDeadlinesAsync(string schema, OperationsDbContext db, CancellationToken ct)
    {
        var now   = DateTime.UtcNow;
        var dirty = false;

        var overdue = await db.NegligenceIncidents
            .Where(i => (i.Status == NegligenceStatus.Logged || i.Status == NegligenceStatus.UnderReview)
                     && i.ResponseDeadline < now
                     && i.ResponseBreachAlertedAt == null)
            .ToListAsync(ct);
        foreach (var i in overdue)
        {
            i.EscalatedToMdAt         = now;
            i.ResponseBreachAlertedAt = now;
            dirty = true;
            logger.LogWarning("Negligence response overdue [{Schema}] {Number} (employee {Emp}) — 5-day window passed, escalated to MD",
                schema, i.IncidentNumber, i.EmployeeId);
        }

        var overrunCutoff = now.Date.AddDays(-14);
        var overrun = await db.Projects
            .Where(p => p.Status == ProjectStatus.Active
                     && p.ActualEndDate == null
                     && p.ExpectedEndDate < overrunCutoff
                     && p.OverrunNoticedAt == null)
            .ToListAsync(ct);
        foreach (var p in overrun)
        {
            var days = (now.Date - p.ExpectedEndDate.Date).Days;
            db.ProgramOverrunNotices.Add(new ProgramOverrunNotice
            {
                ProjectId       = p.Id,
                ExpectedEndDate = p.ExpectedEndDate,
                DaysOverrun     = days,
                Message         = $"Project '{p.Name}' is {days} days past its expected end date ({p.ExpectedEndDate:yyyy-MM-dd}).",
                CreatedBy       = "system",
                UpdatedBy       = "system",
            });
            p.OverrunNoticedAt = now;
            dirty = true;
            logger.LogWarning("Program overrun [{Schema}] project {Id} — {Days}d past expected end ({Due:yyyy-MM-dd})",
                schema, p.Id, days, p.ExpectedEndDate);
        }

        if (dirty) await db.SaveChangesAsync(ct);
    }
}
