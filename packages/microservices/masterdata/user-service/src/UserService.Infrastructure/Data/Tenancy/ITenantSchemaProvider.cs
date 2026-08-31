namespace UserService.Infrastructure.Data.Tenancy;

/// <summary>
/// Carries the Postgres schema for the current unit of work. Phase 2 provisioning binds the schema
/// directly on the connection string (<c>search_path</c>); this scoped provider is the groundwork for
/// Phase 3's request-time interceptor, which will read the resolved tenant off the request and issue
/// <c>SET search_path</c> on the connection. <c>null</c> means "no tenant bound" (control-plane work).
/// </summary>
public interface ITenantSchemaProvider
{
    /// <summary>The tenant schema (e.g. <c>tenant_acme</c>), or null when unbound.</summary>
    string? Schema { get; set; }
}

/// <summary>Default scoped implementation. Set <see cref="Schema"/> before resolving a TenantDbContext.</summary>
public sealed class TenantSchemaProvider : ITenantSchemaProvider
{
    public string? Schema { get; set; }
}
