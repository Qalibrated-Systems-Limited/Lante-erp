using Microsoft.EntityFrameworkCore;

namespace OperationsService.Infrastructure.Data;

/// <summary>
/// Schema-per-tenant variant of <see cref="OperationsDbContext"/>. The base model is already
/// schema-agnostic; this subclass exists to own a separate migration lineage (Migrations/Tenant)
/// so tenant schemas are created clean, without the public-plane's RLS/TenantId policy migrations.
/// The tenant schema is bound via the connection's Postgres search_path during provisioning.
/// </summary>
public class TenantOperationsDbContext : OperationsDbContext
{
    public TenantOperationsDbContext(DbContextOptions<TenantOperationsDbContext> options) : base(options) { }
}
