using Microsoft.EntityFrameworkCore;

namespace HSEService.Infrastructure.Data;

/// <summary>
/// Schema-per-tenant variant of <see cref="HSEDbContext"/>. Inherits the whole model but drops
/// the default schema so it's schema-agnostic — the tenant schema is bound via the connection's
/// Postgres search_path (see <see cref="TenantDbConnectionInterceptor"/>). Own migration lineage
/// (Migrations/Tenant) so tenant schemas provision clean. Mirrors TicketingService exactly.
/// </summary>
public class TenantHSEDbContext : HSEDbContext
{
    public TenantHSEDbContext(DbContextOptions<TenantHSEDbContext> options) : base(options) { }

    protected override string? DefaultSchema => null;
}
