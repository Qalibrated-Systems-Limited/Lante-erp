using Microsoft.EntityFrameworkCore;

namespace TicketingService.Infrastructure.Data;

/// <summary>
/// Schema-per-tenant variant of <see cref="TicketingDbContext"/>. Inherits the entire model
/// (DbSets, configurations, relationships, type mappings) but drops the default schema so it is
/// schema-agnostic — the tenant schema is bound via the connection's Postgres search_path during
/// provisioning. Has its own migration lineage (Migrations/Tenant) so tenant schemas are created
/// clean, without the public-plane's RLS/TenantId policy migrations.
/// </summary>
public class TenantTicketingDbContext : TicketingDbContext
{
    public TenantTicketingDbContext(DbContextOptions<TenantTicketingDbContext> options) : base(options) { }

    protected override string? DefaultSchema => null;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        // Alerts are a shared platform-wide table that lives only in `public` (reached via the
        // search_path fallback), keyed by TenantId — never per-tenant. Exclude it from the
        // tenant lineage so provisioning never creates a shadow tenant_<slug>.Alerts table that
        // would eclipse public.Alerts for that tenant.
        modelBuilder.Ignore<Core.Entities.Alert>();
    }
}
