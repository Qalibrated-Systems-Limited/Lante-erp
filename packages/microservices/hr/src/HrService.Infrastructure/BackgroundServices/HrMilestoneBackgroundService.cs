using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using HrService.Core.Interfaces.Services;
using HrService.Infrastructure.Data;

namespace HrService.Infrastructure.BackgroundServices;

/// <summary>
/// The daily HR sweep, per tenant schema. H2 (HR-003/HR-006): raises probation reviews at Day 90 and Day 180,
/// opens fixed-term contract alerts, and fires the 30-day and 7-day expiry warnings. H3 (HR-005): assigns the
/// current year's leave entitlements, runs the 31 December carry-forward and expires carried days after 31 March.
/// <para>Tenant scoping has no HTTP context here, so the connection is opened explicitly and
/// <c>search_path</c> is pointed at each tenant for the duration of that tenant's work — the interceptor would
/// otherwise leave it on <c>public</c>. The domain service is resolved from the <b>same scope</b> so its
/// repositories share that bound DbContext, and the connection is held open because letting it return to the
/// pool would silently reset the search path. (Same shape as the procurement performance-review worker and the
/// compliance statutory calendar.)</para>
/// <para>Everything it does is idempotent — milestone rows are unique per (employee, type, date) and the alert
/// stamps are one-way — so a daily tick raises nothing twice and a service that was down simply catches up.</para>
/// </summary>
public class HrMilestoneBackgroundService(
    IServiceScopeFactory scopeFactory,
    IConfiguration config,
    ILogger<HrMilestoneBackgroundService> logger) : BackgroundService
{
    private const string SystemActor = "system-scheduler";

    private bool Enabled => config.GetValue("Hr:MilestoneSweep:Enabled", true);
    private TimeSpan Interval => TimeSpan.FromHours(
        Math.Clamp(config.GetValue("Hr:MilestoneSweep:IntervalHours", 24), 1, 168));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!Enabled)
        {
            logger.LogInformation("HR milestone sweep disabled by configuration.");
            return;
        }

        await Task.Delay(TimeSpan.FromSeconds(45), stoppingToken);   // let migrations finish first

        while (!stoppingToken.IsCancellationRequested)
        {
            foreach (var schema in await GetTenantSchemasAsync(stoppingToken))
            {
                if (stoppingToken.IsCancellationRequested) break;
                try
                {
                    await SweepSchemaAsync(schema, stoppingToken);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "HR milestone sweep failed for schema {Schema} — will retry next cycle.", schema);
                }
            }

            try { await Task.Delay(Interval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task<List<string>> GetTenantSchemasAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HrDbContext>();
        return await db.Database
            .SqlQueryRaw<string>("SELECT schema_name FROM information_schema.schemata WHERE schema_name ~ '^tenant_'")
            .ToListAsync(ct);
    }

    private async Task SweepSchemaAsync(string schema, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HrDbContext>();

        await db.Database.OpenConnectionAsync(ct);
        try
        {
#pragma warning disable EF1002 // schema is sourced from information_schema.schemata (not user input, see GetTenantSchemasAsync) — identifiers can't be parameterized via ExecuteSqlAsync anyway.
            await db.Database.ExecuteSqlRawAsync($"SET search_path TO \"{schema}\", public", ct);
#pragma warning restore EF1002

            // The schema is passed through so alert delivery can address the right tenant — there is no
            // request context to infer it from out here.
            var probation = scope.ServiceProvider.GetRequiredService<IProbationService>();
            var result = await probation.RunMilestoneSweepAsync(schema, SystemActor);

            if (result.ProbationReviewsRaised + result.FollowUpReviewsAlerted + result.ContractAlertsOpened
                + result.Alert30Fired + result.Alert7Fired + result.ExpiredFlagged > 0)
                logger.LogInformation("HR milestone sweep for {Schema}: {Message}", schema, result.Message);

            // H3 — leave entitlements, the 31 December carry-forward and the 31 March expiry. Run in the same
            // scope and search_path, and kept separate from the milestone sweep so a failure in one does not
            // cost the other its run.
            try
            {
                var leave = scope.ServiceProvider.GetRequiredService<ILeaveService>();
                var leaveResult = await leave.RunLeaveSweepAsync(schema, SystemActor);

                if (leaveResult.EntitlementsAssigned + leaveResult.CarryForwardRecordsWritten
                    + leaveResult.CarryForwardExpired > 0)
                    logger.LogInformation("HR leave sweep for {Schema}: {Message}", schema, leaveResult.Message);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "HR leave sweep failed for schema {Schema} — will retry next cycle.", schema);
            }

            // H4 — absence detection, pattern alerts and the monthly report/scorecard generation. Runs after
            // leave so an absence is reconciled against leave that has just been approved, and is isolated for
            // the same reason as above.
            try
            {
                var attendance = scope.ServiceProvider.GetRequiredService<IAttendanceService>();
                var attendanceResult = await attendance.RunAttendanceSweepAsync(schema, SystemActor);

                if (attendanceResult.AbsencesDetected + attendanceResult.PatternAlertsRaised
                    + attendanceResult.MonthlyReportsGenerated + attendanceResult.DaysClosedWithoutClockOut > 0)
                    logger.LogInformation("HR attendance sweep for {Schema}: {Message}", schema, attendanceResult.Message);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "HR attendance sweep failed for schema {Schema} — will retry next cycle.", schema);
            }

            // H7 — the L&D red flags: the LDP deadline and its MD escalation, zero training hours by 30 June,
            // mandatory training breached beyond its grace period, the monthly knowledge-sharing shortfall and
            // the budget thresholds. Runs last because its compliance pass calls out to hse and compliance,
            // and a slow or unreachable sibling must not delay the sweeps that only touch HR's own data.
            try
            {
                var learning = scope.ServiceProvider.GetRequiredService<ILearningService>();
                var learningResult = await learning.RunLearningSweepAsync(schema, SystemActor);

                if (learningResult.LdpRemindersSent + learningResult.LdpEscalations + learningResult.ZeroHoursFlags
                    + learningResult.MandatoryBreachAlerts + learningResult.KnowledgeSharingShortfalls
                    + learningResult.BudgetWarnings + learningResult.BudgetsExhausted > 0)
                    logger.LogInformation(
                        "HR learning sweep for {Schema}: {Reminders} LDP reminder(s), {Escalations} escalation(s), {Zero} zero-hours flag(s), {Breaches} mandatory breach(es), {Ks} knowledge-sharing shortfall(s), {Budget} budget alert(s).",
                        schema, learningResult.LdpRemindersSent, learningResult.LdpEscalations, learningResult.ZeroHoursFlags,
                        learningResult.MandatoryBreachAlerts, learningResult.KnowledgeSharingShortfalls,
                        learningResult.BudgetWarnings + learningResult.BudgetsExhausted);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "HR learning sweep failed for schema {Schema} — will retry next cycle.", schema);
            }

            // H10 — warnings past expiry, the three-in-twelve-months termination review, show-cause windows
            // that have lapsed, and grievances past their acknowledgement SLA. Isolated like the others so one
            // failing sweep never costs the rest their run.
            try
            {
                var discipline = scope.ServiceProvider.GetRequiredService<IDisciplineService>();
                var disciplineResult = await discipline.RunDisciplineSweepAsync(schema, SystemActor);

                if (disciplineResult.WarningsExpired + disciplineResult.WarningEscalations
                    + disciplineResult.ShowCauseOverdueAlerts + disciplineResult.GrievanceSlaBreaches > 0)
                    logger.LogInformation(
                        "HR discipline sweep for {Schema}: {Expired} warning(s) expired, {Escalations} termination review(s), {ShowCause} show-cause overdue, {Sla} grievance SLA breach(es).",
                        schema, disciplineResult.WarningsExpired, disciplineResult.WarningEscalations,
                        disciplineResult.ShowCauseOverdueAlerts, disciplineResult.GrievanceSlaBreaches);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "HR discipline sweep failed for schema {Schema} — will retry next cycle.", schema);
            }

            // H11/COM-004 — the quarterly commission statements. Runs last for the same reason the learning
            // sweep runs late: it calls out to crm-service, and a slow sibling must not delay HR's own sweeps.
            // On 363 days of the year this finds the quarter already issued and does nothing.
            try
            {
                var commission = scope.ServiceProvider.GetRequiredService<ICommissionService>();
                var commissionResult = await commission.RunCommissionSweepAsync(schema, SystemActor, ct);

                if (commissionResult.StatementsIssued > 0)
                    logger.LogInformation(
                        "HR commission sweep for {Schema}: {Issued} statement(s) issued for {Year} Q{Quarter}, {Flags} red flag(s).",
                        schema, commissionResult.StatementsIssued, commissionResult.Year,
                        commissionResult.Quarter, commissionResult.RedFlagsRaised);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "HR commission sweep failed for schema {Schema} — will retry next cycle.", schema);
            }

            // H12 — vacancies past their closing date and offers nobody answered. Both free a seat that would
            // otherwise sit held by a candidate who has gone quiet.
            try
            {
                var recruitment = scope.ServiceProvider.GetRequiredService<IRecruitmentService>();
                var recruitmentResult = await recruitment.RunRecruitmentSweepAsync(schema, SystemActor);

                if (recruitmentResult.VacanciesClosed + recruitmentResult.OffersLapsed > 0)
                    logger.LogInformation(
                        "HR recruitment sweep for {Schema}: {Closed} vacancy(ies) closed, {Lapsed} offer(s) lapsed, {Overdue} interview(s) unscored.",
                        schema, recruitmentResult.VacanciesClosed, recruitmentResult.OffersLapsed,
                        recruitmentResult.InterviewsOverdue);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "HR recruitment sweep failed for schema {Schema} — will retry next cycle.", schema);
            }
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }
}
