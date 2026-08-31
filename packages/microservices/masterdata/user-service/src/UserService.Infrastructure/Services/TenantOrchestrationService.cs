using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UserService.Core.Constants;
using UserService.Core.Entities;
using UserService.Core.Interfaces.Services;
using UserService.Infrastructure.Data;

namespace UserService.Infrastructure.Services;

/// <summary>
/// See <see cref="ITenantOrchestrator"/>. Provisions user-service in-process, then fans out to each
/// business service's internal /provision endpoint over HTTP, in parallel. Base URLs come from
/// config <c>ProvisioningTargets:&lt;serviceKey&gt;</c>; the shared secret from
/// <c>InternalServices:ServiceKey</c>. A service with no configured target is left Pending
/// (skipped, logged) rather than marked Failed — it simply hasn't been wired for this environment
/// yet.
/// </summary>
public class TenantOrchestrationService : ITenantOrchestrator
{
    private readonly LanteUserServiceDbContext _controlPlane;
    private readonly ITenantProvisioningService _userProvisioning;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TenantOrchestrationService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public TenantOrchestrationService(
        LanteUserServiceDbContext controlPlane,
        ITenantProvisioningService userProvisioning,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<TenantOrchestrationService> logger,
        IServiceScopeFactory scopeFactory)
    {
        _controlPlane = controlPlane;
        _userProvisioning = userProvisioning;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    public async Task<IReadOnlyList<OrchestrationResult>> ProvisionAllAsync(
        string tenantId, IEnumerable<string>? onlyServiceKeys = null, CancellationToken cancellationToken = default)
    {
        var results = new List<OrchestrationResult>();

        var tenant = await _controlPlane.Tenants.IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken);
        if (tenant is null)
            return new[] { new OrchestrationResult(PlatformServices.User, false, "Tenant not found.") };

        // The tenant's business service set = its existing tracker rows (seeded at creation from
        // the plan or an explicit selection). If none exist yet, fall back to the plan's FeaturesJson.
        var subscribedBusiness = await _controlPlane.TenantServiceSchemas.IgnoreQueryFilters()
            .Where(ts => ts.TenantId == tenantId && ts.ServiceKey != PlatformServices.User)
            .Select(ts => ts.ServiceKey)
            .ToListAsync(cancellationToken);
        subscribedBusiness = subscribedBusiness.Where(PlatformServices.Business.Contains).ToList();

        if (subscribedBusiness.Count == 0)
        {
            var featuresJson = await _controlPlane.CompanySubscriptions.IgnoreQueryFilters()
                .Where(s => s.TenantId == tenantId)
                .OrderByDescending(s => s.CreatedAt)
                .Select(s => s.Plan!.FeaturesJson)
                .FirstOrDefaultAsync(cancellationToken);
            subscribedBusiness = PlatformServices.ResolveSubscribed(featuresJson)
                .Where(k => k != PlatformServices.User)
                .ToList();
        }

        if (onlyServiceKeys != null)
        {
            var filter = onlyServiceKeys.ToHashSet();
            subscribedBusiness = subscribedBusiness.Where(filter.Contains).ToList();
        }

        _logger.LogInformation("Tenant {TenantId} business services to provision: [{Services}]",
            tenantId, string.Join(", ", subscribedBusiness));

        // 1. user-service in-process (updates its own tracker row). Only runs on a full pass —
        // a scoped retry-failed run only touches the business services named in onlyServiceKeys.
        if (onlyServiceKeys is null)
        {
            var userResult = await _userProvisioning.ProvisionUserSchemaAsync(tenantId, cancellationToken);
            results.Add(new OrchestrationResult(userResult.ServiceKey, userResult.Success, userResult.Error));
        }

        // 2. subscribed business services over HTTP, in parallel. Each task gets its own DI scope
        // (and therefore its own DbContext) — the shared _controlPlane context used above is not
        // thread-safe and must not be touched concurrently from here on.
        var serviceKey = _configuration["InternalServices:ServiceKey"];
        var businessResults = await Task.WhenAll(subscribedBusiness.Select(svc =>
            ProvisionBusinessServiceAsync(tenantId, tenant.SchemaName, svc, serviceKey, cancellationToken)));
        results.AddRange(businessResults.Where(r => r != null)!);

        return results;
    }

    /// <summary>
    /// Provisions one business service for one tenant. Runs inside its own DI scope so concurrent
    /// calls (from <see cref="Task.WhenAll"/> here, and from a separate caller — e.g. the background
    /// retry sweep racing a human's manual re-provision click) never share a DbContext instance.
    /// The atomic claim below (an UPDATE ... WHERE Status != Provisioning) is what actually prevents
    /// two callers from double-processing the same (tenant, service) row concurrently — returns null
    /// if another caller already holds the claim, rather than racing writes to the same tracker.
    /// </summary>
    private async Task<OrchestrationResult?> ProvisionBusinessServiceAsync(
        string tenantId, string schemaName, string svc, string? serviceKey, CancellationToken cancellationToken)
    {
        var baseUrl = _configuration[$"ProvisioningTargets:{svc}"];
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            _logger.LogWarning("No ProvisioningTargets:{Service} configured; leaving it Pending.", svc);
            return new OrchestrationResult(svc, false, "No provisioning target configured.");
        }

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LanteUserServiceDbContext>();

        await EnsureTrackerExistsAsync(db, tenantId, svc, schemaName, cancellationToken);

        var claimed = await db.TenantServiceSchemas.IgnoreQueryFilters()
            .Where(ts => ts.TenantId == tenantId && ts.ServiceKey == svc && ts.Status != ProvisioningStatus.Provisioning)
            .ExecuteUpdateAsync(s => s
                .SetProperty(ts => ts.Status, ProvisioningStatus.Provisioning)
                .SetProperty(ts => ts.LastError, (string?)null)
                .SetProperty(ts => ts.UpdatedAt, DateTime.UtcNow), cancellationToken);

        if (claimed == 0)
        {
            _logger.LogInformation(
                "Skipping {Service} for tenant {TenantId}: already being provisioned by another caller.", svc, tenantId);
            return null;
        }

        try
        {
            var client = _httpClientFactory.CreateClient("provisioning");
            client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
            if (!string.IsNullOrEmpty(serviceKey))
                client.DefaultRequestHeaders.Add("X-Internal-Key", serviceKey);

            var response = await client.PostAsJsonAsync(
                "internal/tenants/provision",
                new { tenantId, schema = schemaName },
                cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                await db.TenantServiceSchemas.IgnoreQueryFilters()
                    .Where(ts => ts.TenantId == tenantId && ts.ServiceKey == svc)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(ts => ts.Status, ProvisioningStatus.Provisioned)
                        .SetProperty(ts => ts.ProvisionedAt, DateTime.UtcNow)
                        .SetProperty(ts => ts.LastError, (string?)null)
                        .SetProperty(ts => ts.RetryCount, 0)
                        .SetProperty(ts => ts.UpdatedAt, DateTime.UtcNow), cancellationToken);
                return new OrchestrationResult(svc, true, null);
            }
            else
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                var error = Truncate($"HTTP {(int)response.StatusCode}: {body}", 1000);
                await MarkFailedAsync(db, tenantId, svc, error, cancellationToken);
                return new OrchestrationResult(svc, false, error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Provisioning call to {Service} failed for tenant {TenantId}", svc, tenantId);
            await MarkFailedAsync(db, tenantId, svc, Truncate(ex.Message, 1000), cancellationToken);
            return new OrchestrationResult(svc, false, ex.Message);
        }
    }

    private static async Task MarkFailedAsync(LanteUserServiceDbContext db, string tenantId, string svc, string error, CancellationToken ct)
    {
        await db.TenantServiceSchemas.IgnoreQueryFilters()
            .Where(ts => ts.TenantId == tenantId && ts.ServiceKey == svc)
            .ExecuteUpdateAsync(s => s
                .SetProperty(ts => ts.Status, ProvisioningStatus.Failed)
                .SetProperty(ts => ts.LastError, error)
                .SetProperty(ts => ts.RetryCount, ts => ts.RetryCount + 1)
                .SetProperty(ts => ts.UpdatedAt, DateTime.UtcNow), ct);
    }

    private static async Task EnsureTrackerExistsAsync(LanteUserServiceDbContext db, string tenantId, string serviceKey, string schema, CancellationToken ct)
    {
        var exists = await db.TenantServiceSchemas.IgnoreQueryFilters()
            .AnyAsync(ts => ts.TenantId == tenantId && ts.ServiceKey == serviceKey, ct);
        if (exists) return;

        db.TenantServiceSchemas.Add(new TenantServiceSchema
        {
            TenantId = tenantId,
            ServiceKey = serviceKey,
            SchemaName = schema,
            Status = ProvisioningStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Lost the create race to a concurrent caller — the row exists now either way.
            db.ChangeTracker.Clear();
        }
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];
}
