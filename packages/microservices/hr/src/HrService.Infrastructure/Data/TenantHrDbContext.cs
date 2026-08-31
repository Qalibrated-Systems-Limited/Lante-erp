using Microsoft.EntityFrameworkCore;

namespace HrService.Infrastructure.Data;

/// <summary>
/// Schema-per-tenant variant of <see cref="HrDbContext"/>. The base model is already schema-agnostic;
/// this subclass owns a separate migration lineage (Migrations/Tenant) so tenant schemas are created
/// clean, without the public-plane's migrations. The tenant schema is bound via the connection's
/// Postgres search_path during provisioning.
/// </summary>
public class TenantHrDbContext : HrDbContext
{
    public TenantHrDbContext(DbContextOptions<TenantHrDbContext> options) : base(options) { }
}
