namespace UserService.Core.Interfaces.Services;

/// <summary>Outcome of provisioning a single service's tenant schema.</summary>
public record ProvisioningResult(bool Success, string ServiceKey, string Schema, string? Error);

/// <summary>
/// Provisions the user-service tenant plane: creates the tenant's Postgres schema, migrates the
/// tenant-plane tables into it, seeds standard data, and records status on the control-plane
/// TenantServiceSchema tracker. Phase 2 handles the user-service's own schema in-process; other
/// services are provisioned over their internal /provision endpoints by the orchestrator.
/// </summary>
public interface ITenantProvisioningService
{
    /// <summary>
    /// Provision (or re-run, idempotently) the user-service schema for the given control-plane tenant.
    /// </summary>
    Task<ProvisioningResult> ProvisionUserSchemaAsync(string tenantId, CancellationToken cancellationToken = default);
}
