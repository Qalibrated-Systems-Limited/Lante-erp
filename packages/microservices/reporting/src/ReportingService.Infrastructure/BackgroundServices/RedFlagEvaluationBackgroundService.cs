using Microsoft.EntityFrameworkCore;
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
/// RPT-011/012: evaluates every active RedFlagRule against its bound DataSource's current value
/// (via MetricResolverService — the same resolution path DataSourcesController/DashboardsController/
/// KpiScorecardsController use). A rule whose condition holds and has no open RedFlagEvent gets a
/// new one plus a ticketing alert (ComplianceAlertsBackgroundService's exact CreateAlertAsync
/// pattern); one that's already open just has DaysOpen refreshed; one whose condition no longer
/// holds gets its open event auto-resolved. Runs every 15 minutes — the same cadence
/// ReportSchedulerBackgroundService uses, since these read live operational metrics rather than
/// Compliance's date-driven statutory deadlines.
/// </summary>
public class RedFlagEvaluationBackgroundService(IServiceScopeFactory scopeFactory, ILogger<RedFlagEvaluationBackgroundService> logger)
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
                    await EvaluateSchemaAsync(schema);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Red-flag evaluation failed for schema {Schema} — will retry next cycle", schema);
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

    private async Task EvaluateSchemaAsync(string schema)
    {
        using var scope = scopeFactory.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<ReportingDbContext>();
        var resolver = sp.GetRequiredService<MetricResolverService>();
        var ticketingClient = sp.GetRequiredService<ITicketingServiceClient>();

        await db.Database.OpenConnectionAsync();
#pragma warning disable EF1002 // schema is sourced from information_schema.schemata (GetTenantSchemasAsync), not user input, and identifiers can't be parameterized via ExecuteSqlAsync anyway.
        await db.Database.ExecuteSqlRawAsync($"SET search_path TO \"{schema}\", public");
#pragma warning restore EF1002

        var now = DateTime.UtcNow;
        var rules = await db.RedFlagRules.Where(r => r.IsActive).ToListAsync();
        var dataSourceLookup = (await db.DataSources.ToListAsync()).ToDictionary(d => d.Id, d => d);

        var raised = 0;
        var resolved = 0;
        foreach (var rule in rules)
        {
            if (!dataSourceLookup.TryGetValue(rule.DataSourceId, out var dataSource))
            {
                logger.LogWarning("RedFlagRule {RuleId} references missing DataSource {DataSourceId}", rule.Id, rule.DataSourceId);
                continue;
            }

            decimal value;
            try
            {
                value = await resolver.ResolveAsync(sp, dataSource.MetricKey);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed resolving {MetricKey} for RedFlagRule {RuleId} in schema {Schema}", dataSource.MetricKey, rule.Id, schema);
                continue;
            }

            var conditionMet = Evaluate(rule.Condition, value, rule.ThresholdValue);
            var openEvent = await db.RedFlagEvents
                .Where(e => e.RedFlagRuleId == rule.Id && e.Status == RedFlagEventStatus.Open)
                .OrderByDescending(e => e.TriggeredAt)
                .FirstOrDefaultAsync();

            if (conditionMet)
            {
                if (openEvent == null)
                {
                    var newEvent = new RedFlagEvent
                    {
                        RedFlagRuleId = rule.Id,
                        TriggeredAt = now,
                        Severity = rule.Severity,
                        Value = value,
                        DaysOpen = 0,
                        Status = RedFlagEventStatus.Open,
                    };
                    db.RedFlagEvents.Add(newEvent);
                    raised++;

                    await ticketingClient.CreateAlertAsync(schema, "RedFlag", rule.Severity.ToString(),
                        $"Red flag triggered — {rule.Name}",
                        $"{dataSource.Name} is {value} ({rule.Condition} {rule.ThresholdValue}).",
                        requiredPermission: "reports.view");
                }
                else
                {
                    openEvent.Value = value;
                    openEvent.DaysOpen = (int)(now - openEvent.TriggeredAt).TotalDays;
                    db.RedFlagEvents.Update(openEvent);
                }
            }
            else if (openEvent != null)
            {
                openEvent.ResolvedAt = now;
                openEvent.Status = RedFlagEventStatus.Resolved;
                openEvent.DaysOpen = (int)(now - openEvent.TriggeredAt).TotalDays;
                db.RedFlagEvents.Update(openEvent);
                resolved++;
            }
        }

        await db.SaveChangesAsync();
        if (raised + resolved > 0)
            logger.LogInformation("Red-flag evaluation in {Schema}: {Raised} raised, {Resolved} resolved", schema, raised, resolved);

        await db.Database.CloseConnectionAsync();
    }

    private static bool Evaluate(RedFlagCondition condition, decimal value, decimal threshold) => condition switch
    {
        RedFlagCondition.GreaterThan => value > threshold,
        RedFlagCondition.GreaterThanOrEqual => value >= threshold,
        RedFlagCondition.LessThan => value < threshold,
        RedFlagCondition.LessThanOrEqual => value <= threshold,
        RedFlagCondition.Equals => value == threshold,
        _ => throw new InvalidOperationException($"Unknown RedFlagCondition '{condition}'."),
    };
}
