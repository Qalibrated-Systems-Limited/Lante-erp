using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ProcurementService.Core.DTOs.Performance;
using ProcurementService.Core.Interfaces.Services;
using ProcurementService.Infrastructure.Data;

namespace ProcurementService.Infrastructure.BackgroundServices;

/// <summary>
/// P9 (PROC-002) — runs the biannual supplier performance review automatically, per tenant schema.
/// <para>The review is scored for the most recently <b>completed</b> half (in H2 it scores this year's H1; in
/// H1 it scores last year's H2), because a half still in progress has incomplete transaction data. The
/// in-progress half can still be scored on demand from the Performance tab.</para>
/// <para>It ticks daily rather than being scheduled for a single date: on each tick it asks whether any
/// eligible supplier is missing a review for the completed half, and only then runs. That makes it
/// idempotent, self-healing (a service that was down through the turn of the half still catches up when it
/// returns) and correct for suppliers approved after the first run.</para>
/// <para>Tenant scoping mirrors the compliance calendar worker: there is no HTTP context here, so the
/// connection is opened explicitly and <c>search_path</c> is pointed at each tenant schema for the duration
/// of that tenant's work — the interceptor would otherwise leave it on public.</para>
/// </summary>
public class PerformanceReviewBackgroundService(
    IServiceScopeFactory scopeFactory,
    IConfiguration config,
    ILogger<PerformanceReviewBackgroundService> logger) : BackgroundService
{
    private const string SystemActor = "system-scheduler";

    private bool Enabled => config.GetValue("Procurement:PerformanceReview:Enabled", true);
    private TimeSpan Interval => TimeSpan.FromHours(
        Math.Clamp(config.GetValue("Procurement:PerformanceReview:IntervalHours", 24), 1, 168));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!Enabled)
        {
            logger.LogInformation("Biannual performance review scheduler disabled by configuration.");
            return;
        }

        await Task.Delay(TimeSpan.FromSeconds(45), stoppingToken);   // let migrations finish first

        while (!stoppingToken.IsCancellationRequested)
        {
            var period = LastCompletedHalf(DateTime.UtcNow);
            foreach (var schema in await GetTenantSchemasAsync(stoppingToken))
            {
                if (stoppingToken.IsCancellationRequested) break;
                try
                {
                    await ProcessSchemaAsync(schema, period, stoppingToken);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Biannual performance review failed for schema {Schema} — will retry next cycle.", schema);
                }
            }

            try { await Task.Delay(Interval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    /// <summary>"YYYY-H1"/"YYYY-H2" for the half that has already ended.</summary>
    internal static string LastCompletedHalf(DateTime now)
        => now.Month <= 6 ? $"{now.Year - 1}-H2" : $"{now.Year}-H1";

    private async Task<List<string>> GetTenantSchemasAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProcurementDbContext>();
        return await db.Database
            .SqlQueryRaw<string>("SELECT schema_name FROM information_schema.schemata WHERE schema_name ~ '^tenant_'")
            .ToListAsync(ct);
    }

    private async Task ProcessSchemaAsync(string schema, string period, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProcurementDbContext>();

        // No HTTP context here, so bind this connection to the tenant ourselves and hold it open — the
        // service resolved below shares this DbContext, and letting the connection return to the pool would
        // reset search_path back to public.
        await db.Database.OpenConnectionAsync(ct);
        try
        {
#pragma warning disable EF1002 // schema is sourced from information_schema.schemata (filtered to '^tenant_') in GetTenantSchemasAsync above, not user input — and identifiers can't be parameterized via ExecuteSqlAsync anyway.
            await db.Database.ExecuteSqlRawAsync($"SET search_path TO \"{schema}\", public", ct);
#pragma warning restore EF1002

            if (!await IsDueAsync(db, period, ct)) return;

            var service = scope.ServiceProvider.GetRequiredService<IPerformanceReviewService>();
            var result = await service.RunAsync(new RunReviewDto { Period = period }, SystemActor);
            logger.LogInformation("Biannual performance review {Period} for {Schema}: {Message}", period, schema, result.Message);
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    /// <summary>Due when at least one approved, non-blacklisted supplier has no review for the period —
    /// which also covers suppliers approved after an earlier run.</summary>
    private static async Task<bool> IsDueAsync(ProcurementDbContext db, string period, CancellationToken ct)
    {
        var eligible = await db.Suppliers.AsNoTracking()
            .Where(s => !s.IsDeleted && s.IsApproved && !s.BlacklistFlag)
            .Select(s => s.Id).ToListAsync(ct);
        if (eligible.Count == 0) return false;

        var reviewed = await db.SupplierPerformanceReviews.AsNoTracking()
            .Where(r => !r.IsDeleted && r.ReviewPeriod == period)
            .Select(r => r.SupplierId).ToListAsync(ct);

        return eligible.Except(reviewed).Any();
    }
}
