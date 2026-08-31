using Microsoft.EntityFrameworkCore;

namespace StoreService.Infrastructure.Data;

/// <summary>Schema-per-tenant variant of <see cref="StoreDbContext"/>. Schema-agnostic model, owns
/// a separate migration lineage (Migrations/Tenant). The tenant schema is bound via the
/// connection's Postgres search_path.</summary>
public class TenantStoreDbContext : StoreDbContext
{
    public TenantStoreDbContext(DbContextOptions<TenantStoreDbContext> options) : base(options) { }

    protected override string? DefaultSchema => null;
}
