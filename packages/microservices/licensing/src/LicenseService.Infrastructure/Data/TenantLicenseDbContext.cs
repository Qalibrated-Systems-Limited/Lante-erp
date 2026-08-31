using Microsoft.EntityFrameworkCore;

namespace LicenseService.Infrastructure.Data;

/// <summary>
/// Schema-per-tenant variant of <see cref="LanteLicenseDbContext"/>. Drops the fixed "licensing"
/// default schema so the model is schema-agnostic, and owns a separate migration lineage
/// (Migrations/Tenant). The tenant schema is bound via the connection's Postgres search_path.
/// </summary>
public class TenantLicenseDbContext : LanteLicenseDbContext
{
    public TenantLicenseDbContext(DbContextOptions<TenantLicenseDbContext> options) : base(options) { }

    protected override string? DefaultSchema => null;
}
