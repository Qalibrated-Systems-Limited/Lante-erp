using Microsoft.EntityFrameworkCore;

namespace FleetService.Infrastructure.Data;

/// <summary>
/// Schema-per-tenant variant of <see cref="FleetServiceDbContext"/>. Owns a separate migration
/// lineage (Migrations/Tenant) generated from the CURRENT model — which also sidesteps the base
/// lineage's stale-migration drift (datetime → timestamptz). The tenant schema is bound via the
/// connection's Postgres search_path during provisioning.
/// </summary>
public class TenantFleetServiceDbContext : FleetServiceDbContext
{
    public TenantFleetServiceDbContext(DbContextOptions<TenantFleetServiceDbContext> options) : base(options) { }
}
