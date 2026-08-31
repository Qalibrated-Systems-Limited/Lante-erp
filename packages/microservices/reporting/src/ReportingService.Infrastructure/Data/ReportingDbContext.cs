using System.Linq;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using ReportingService.Core.Entities;

namespace ReportingService.Infrastructure.Data;

public class ReportingDbContext : DbContext
{
    public ReportingDbContext(DbContextOptions<ReportingDbContext> options) : base(options) { }

    /// <summary>Constructor for derived contexts (the schema-per-tenant variant).</summary>
    protected ReportingDbContext(DbContextOptions options) : base(options) { }

    /// <summary>
    /// Null = schema-agnostic: unqualified table names, so the connection's search_path (set by
    /// TenantDbConnectionInterceptor) decides which tenant schema is queried. Same convention as
    /// TicketingDbContext / ComplianceDbContext / SubcontractsDbContext.
    /// </summary>
    protected virtual string? DefaultSchema => null;

    public DbSet<ReportDefinition> ReportDefinitions { get; set; } = null!;
    public DbSet<ReportSchedule> ReportSchedules { get; set; } = null!;
    public DbSet<ReportRecipient> ReportRecipients { get; set; } = null!;
    public DbSet<ReportRun> ReportRuns { get; set; } = null!;
    public DbSet<DataSource> DataSources { get; set; } = null!;
    public DbSet<ReportSource> ReportSources { get; set; } = null!;
    public DbSet<Dashboard> Dashboards { get; set; } = null!;
    public DbSet<DashboardWidget> DashboardWidgets { get; set; } = null!;
    public DbSet<KpiScorecard> KpiScorecards { get; set; } = null!;
    public DbSet<RedFlagRule> RedFlagRules { get; set; } = null!;
    public DbSet<RedFlagEvent> RedFlagEvents { get; set; } = null!;

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

        modelBuilder.Entity<ReportDefinition>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<ReportSchedule>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<ReportRecipient>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<ReportRun>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<DataSource>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<ReportSource>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Dashboard>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<DashboardWidget>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<KpiScorecard>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<RedFlagRule>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<RedFlagEvent>().HasQueryFilter(e => !e.IsDeleted);

        modelBuilder.Entity<ReportSchedule>()
            .HasOne<ReportDefinition>()
            .WithMany()
            .HasForeignKey(s => s.ReportDefinitionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ReportRecipient>()
            .HasOne<ReportSchedule>()
            .WithMany()
            .HasForeignKey(r => r.ReportScheduleId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ReportRun>()
            .HasOne<ReportDefinition>()
            .WithMany()
            .HasForeignKey(r => r.ReportDefinitionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ReportSource>()
            .HasOne<ReportDefinition>()
            .WithMany()
            .HasForeignKey(s => s.ReportDefinitionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ReportSource>()
            .HasOne<DataSource>()
            .WithMany()
            .HasForeignKey(s => s.DataSourceId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DashboardWidget>()
            .HasOne<Dashboard>()
            .WithMany()
            .HasForeignKey(w => w.DashboardId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DashboardWidget>()
            .HasOne<DataSource>()
            .WithMany()
            .HasForeignKey(w => w.DataSourceId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<KpiScorecard>()
            .HasOne<DataSource>()
            .WithMany()
            .HasForeignKey(s => s.DataSourceId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<RedFlagRule>()
            .HasOne<DataSource>()
            .WithMany()
            .HasForeignKey(r => r.DataSourceId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<RedFlagEvent>()
            .HasOne<RedFlagRule>()
            .WithMany()
            .HasForeignKey(e => e.RedFlagRuleId)
            .OnDelete(DeleteBehavior.Cascade);

        // PostgreSQL type mappings
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
