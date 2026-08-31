using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using UserService.Core.Entities;
using UserService.Core.Interfaces.Services;
using UserService.Infrastructure.Data;

namespace UserService.Infrastructure.Services;

/// <summary>
/// Self-heals tenant provisioning: every sweep, retries any (tenant, service) tracker row still
/// Failed, and reclaims any row stuck Provisioning for too long (a pod that died mid-call, since
/// nothing else would ever flip it back). Mirrors the polling-loop shape of
/// OperationsBackgroundService — see that class for the pattern this follows.
///
/// Capped at <see cref="MaxRetries"/> attempts per row: a genuinely broken/misconfigured service
/// (bad config, service permanently down) must not retry forever. Once capped, the row stays
/// Failed with its RetryCount at the cap — visible on the company detail page and to
/// GET .../provisioning — and only the manual "retry failed" action (no cap) can move it further.
/// </summary>
public class ProvisioningRetryBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<ProvisioningRetryBackgroundService> logger)
    : BackgroundService
{
    private static readonly TimeSpan SweepInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan StuckProvisioningTimeout = TimeSpan.FromMinutes(10);
    private const int MaxRetries = 5;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Provisioning retry background service started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunSweepAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Provisioning retry sweep failed.");
            }
            await Task.Delay(SweepInterval, stoppingToken);
        }

        logger.LogInformation("Provisioning retry background service stopped.");
    }

    private async Task RunSweepAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LanteUserServiceDbContext>();

        // A row stuck Provisioning past the timeout means whatever pod was handling it died
        // mid-call — nothing else will ever flip it back, so without this it would wedge forever
        // (the orchestrator's atomic claim only ever moves a row OUT of Provisioning, never back
        // in on its own). Treat it as a failed attempt and let the retry pass below pick it up.
        var stuckCutoff = DateTime.UtcNow - StuckProvisioningTimeout;
        var reclaimed = await db.TenantServiceSchemas.IgnoreQueryFilters()
            .Where(ts => ts.Status == ProvisioningStatus.Provisioning && ts.UpdatedAt < stuckCutoff)
            .ExecuteUpdateAsync(s => s
                .SetProperty(ts => ts.Status, ProvisioningStatus.Failed)
                .SetProperty(ts => ts.LastError, "Provisioning attempt did not complete (stuck > 10m); treated as failed.")
                .SetProperty(ts => ts.RetryCount, ts => ts.RetryCount + 1)
                .SetProperty(ts => ts.UpdatedAt, DateTime.UtcNow), ct);
        if (reclaimed > 0)
            logger.LogWarning("Reclaimed {Count} tracker row(s) stuck in Provisioning for over {Minutes}m.",
                reclaimed, StuckProvisioningTimeout.TotalMinutes);

        var retryable = await db.TenantServiceSchemas.IgnoreQueryFilters()
            .Where(ts => ts.Status == ProvisioningStatus.Failed && ts.RetryCount < MaxRetries)
            .Select(ts => new { ts.TenantId, ts.ServiceKey })
            .ToListAsync(ct);

        if (retryable.Count == 0) return;

        var orchestrator = scope.ServiceProvider.GetRequiredService<ITenantOrchestrator>();

        foreach (var group in retryable.GroupBy(r => r.TenantId))
        {
            var serviceKeys = group.Select(r => r.ServiceKey).ToList();
            try
            {
                var results = await orchestrator.ProvisionAllAsync(group.Key, serviceKeys, ct);
                var stillFailed = results.Where(r => !r.Success).Select(r => r.ServiceKey).ToList();
                if (stillFailed.Count > 0)
                    logger.LogInformation("Retry sweep: tenant {TenantId} still failing for [{Services}].",
                        group.Key, string.Join(", ", stillFailed));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Retry sweep failed for tenant {TenantId}.", group.Key);
            }
        }
    }
}
