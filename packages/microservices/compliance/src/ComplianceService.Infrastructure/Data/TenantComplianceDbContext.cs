using Microsoft.EntityFrameworkCore;

namespace ComplianceService.Infrastructure.Data;

/// <summary>
/// Schema-per-tenant variant of <see cref="ComplianceDbContext"/>. Inherits the whole model but
/// drops the default schema so it's schema-agnostic — the tenant schema is bound via the
/// connection's Postgres search_path (see <see cref="TenantDbConnectionInterceptor"/>). Own
/// migration lineage (Migrations/Tenant) so tenant schemas provision clean.
/// </summary>
public class TenantComplianceDbContext : ComplianceDbContext
{
    public TenantComplianceDbContext(DbContextOptions<TenantComplianceDbContext> options) : base(options) { }

    protected override string? DefaultSchema => null;
}
