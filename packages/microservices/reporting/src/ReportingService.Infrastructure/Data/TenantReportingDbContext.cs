using Microsoft.EntityFrameworkCore;

namespace ReportingService.Infrastructure.Data;

/// <summary>
/// Schema-per-tenant variant of <see cref="ReportingDbContext"/>. Inherits the whole model but
/// drops the default schema so it's schema-agnostic — the tenant schema is bound via the
/// connection's Postgres search_path (see <see cref="TenantDbConnectionInterceptor"/>). Own
/// migration lineage (Migrations/Tenant) so tenant schemas provision clean.
/// </summary>
public class TenantReportingDbContext : ReportingDbContext
{
    public TenantReportingDbContext(DbContextOptions<TenantReportingDbContext> options) : base(options) { }

    protected override string? DefaultSchema => null;
}
