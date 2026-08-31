namespace UserService.Core.Interfaces.Services;

/// <summary>Per-service result of an orchestrated provisioning run.</summary>
public record OrchestrationResult(string ServiceKey, bool Success, string? Error);

/// <summary>
/// Orchestrates schema-per-tenant provisioning across all services for a tenant: provisions the
/// user-service schema in-process, then calls each business service's internal /provision endpoint
/// over HTTP (authenticated by the shared internal key), updating each control-plane
/// TenantServiceSchema tracker row as it goes.
/// </summary>
public interface ITenantOrchestrator
{
    /// <param name="onlyServiceKeys">
    /// When null (default), provisions every subscribed business service (a full run — e.g. the
    /// manual "Re-provision" action). When given, scopes the run to just these service keys — e.g.
    /// retrying only the services a previous run left Failed, without re-touching healthy ones.
    /// </param>
    Task<IReadOnlyList<OrchestrationResult>> ProvisionAllAsync(string tenantId, IEnumerable<string>? onlyServiceKeys = null, CancellationToken cancellationToken = default);
}
