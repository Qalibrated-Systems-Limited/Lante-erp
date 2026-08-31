using System.Linq;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using SubcontractsService.Core.Entities;

namespace SubcontractsService.Infrastructure.Data;

public class SubcontractsDbContext : DbContext
{
    public SubcontractsDbContext(DbContextOptions<SubcontractsDbContext> options) : base(options) { }

    /// <summary>Constructor for derived contexts (the schema-per-tenant variant).</summary>
    protected SubcontractsDbContext(DbContextOptions options) : base(options) { }

    /// <summary>
    /// Null = schema-agnostic: unqualified table names, so the connection's search_path (set by
    /// TenantDbConnectionInterceptor) decides which tenant schema is queried. Same convention as
    /// TicketingDbContext / HSEDbContext / ComplianceDbContext.
    /// </summary>
    protected virtual string? DefaultSchema => null;

    public DbSet<Subcontractor> Subcontractors { get; set; } = null!;
    public DbSet<Prequalification> Prequalifications { get; set; } = null!;
    public DbSet<SubcontractAward> SubcontractAwards { get; set; } = null!;
    public DbSet<SubconScorecard> SubconScorecards { get; set; } = null!;
    public DbSet<PaymentRetention> PaymentRetentions { get; set; } = null!;
    public DbSet<SubcontractsAuditLog> SubcontractsAuditLogs { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Money-column precision backstop (#383): every decimal defaults to numeric(18,2)
        // unless a naming pattern below says otherwise, or an explicit .HasColumnType(...)
        // further down in this method overrides it for that specific property.
        foreach (var prop in modelBuilder.Model.GetEntityTypes()
                     .SelectMany(t => t.GetProperties())
                     .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
        {
            var name = prop.Name;
            prop.SetColumnType(
                name.Contains("Latitude") || name.Contains("Longitude") ? "numeric(9,6)"
                : name.EndsWith("Pct") || name.EndsWith("Percent") || name.Contains("Probability") ? "numeric(9,4)"
                : name.Contains("ExchangeRate") ? "numeric(18,6)"
                : "numeric(18,2)");
        }

        if (DefaultSchema is not null)
            modelBuilder.HasDefaultSchema(DefaultSchema);
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        modelBuilder.Entity<Subcontractor>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Prequalification>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<SubcontractAward>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<SubconScorecard>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<PaymentRetention>().HasQueryFilter(e => !e.IsDeleted);

        // PostgreSQL type mappings
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTime) || property.ClrType == typeof(DateTime?))
                    property.SetColumnType("TIMESTAMPTZ");
                else if (property.ClrType == typeof(bool))
                    property.SetColumnType("BOOLEAN");
                else if (property.ClrType == typeof(decimal) || property.ClrType == typeof(decimal?))
                    property.SetColumnType("NUMERIC(18,2)");
            }
        }
    }
}
