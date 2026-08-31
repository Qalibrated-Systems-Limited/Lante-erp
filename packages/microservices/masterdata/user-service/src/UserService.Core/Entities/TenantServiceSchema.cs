namespace UserService.Core.Entities;

public enum ProvisioningStatus { Pending, Provisioning, Provisioned, Failed }

/// <summary>
/// Control-plane record tracking the per-service schema for a tenant.
/// One row per (tenant, service) the tenant is provisioned into.
/// The provisioning engine (Phase 2) transitions Status and stamps ProvisionedAt.
/// </summary>
public class TenantServiceSchema : BaseEntity
{
    public string TenantId   { get; set; } = string.Empty;
    // Service key, e.g. "user", "ticketing", "operations", "fleet", "licensing".
    public string ServiceKey { get; set; } = string.Empty;
    // Postgres schema name provisioned in that service's DB, e.g. "tenant_acme".
    public string SchemaName { get; set; } = string.Empty;
    public ProvisioningStatus Status { get; set; } = ProvisioningStatus.Pending;
    public DateTime? ProvisionedAt { get; set; }
    public string?   LastError     { get; set; }
    // Consecutive failed attempts since the last success. Reset to 0 on success. The background
    // retry sweep stops retrying once this hits its cap (see ProvisioningRetryBackgroundService) —
    // at that point the row stays Failed until a human intervenes, so this also doubles as the
    // "needs attention" signal surfaced on the company detail page.
    public int RetryCount { get; set; }

    public virtual Tenant Tenant { get; set; } = null!;
}
