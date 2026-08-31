using System.Reflection;
using LicenseService.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace LicenseService.Infrastructure.Data;

public class LanteLicenseDbContext : DbContext
{
    public LanteLicenseDbContext(DbContextOptions<LanteLicenseDbContext> options) : base(options) { }

    /// <summary>Constructor for derived contexts (e.g. the schema-per-tenant variant).</summary>
    protected LanteLicenseDbContext(DbContextOptions options) : base(options) { }

    /// <summary>
    /// Default Postgres schema. Null = schema-agnostic (Phase 3E): the model emits UNQUALIFIED
    /// names so the connection's search_path decides the schema — the tenant schema when the
    /// interceptor binds it, or the "licensing" fallback (set as the role's default search_path)
    /// for non-tenant requests. The migration-history table stays pinned to "licensing" (where the
    /// existing lineage lives) via MigrationsHistoryTable in Program.cs.
    /// </summary>
    protected virtual string? DefaultSchema => null;

    public DbSet<License> Licenses { get; set; } = null!;
    public DbSet<LicenseAuditLog> LicenseAuditLogs { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        if (DefaultSchema is not null)
            modelBuilder.HasDefaultSchema(DefaultSchema);
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        // Global soft-delete filter
        modelBuilder.Entity<License>().HasQueryFilter(e => !e.IsDeleted);

        // PostgreSQL type mappings — match the convention used across all Lante services
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTime) || property.ClrType == typeof(DateTime?))
                    property.SetColumnType("TIMESTAMPTZ");
                else if (property.ClrType == typeof(bool))
                    property.SetColumnType("BOOLEAN");
            }
        }
    }
}
