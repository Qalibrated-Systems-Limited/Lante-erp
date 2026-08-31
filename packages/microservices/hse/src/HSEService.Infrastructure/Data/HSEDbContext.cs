using System.Reflection;
using Microsoft.EntityFrameworkCore;
using HSEService.Core.Entities;

namespace HSEService.Infrastructure.Data;

public class HSEDbContext : DbContext
{
    public HSEDbContext(DbContextOptions<HSEDbContext> options) : base(options) { }

    /// <summary>Constructor for derived contexts (the schema-per-tenant variant).</summary>
    protected HSEDbContext(DbContextOptions options) : base(options) { }

    /// <summary>
    /// Null = schema-agnostic: unqualified table names, so the connection's search_path (set by
    /// TenantDbConnectionInterceptor) decides which tenant schema is queried. Same convention as
    /// TicketingDbContext.
    /// </summary>
    protected virtual string? DefaultSchema => null;

    public DbSet<Site> Sites { get; set; } = null!;
    public DbSet<HseIncident> HseIncidents { get; set; } = null!;
    public DbSet<EnvIncident> EnvIncidents { get; set; } = null!;
    public DbSet<CorrectiveAction> CorrectiveActions { get; set; } = null!;
    public DbSet<Rams> RamsRecords { get; set; } = null!;
    public DbSet<PpeIssue> PpeIssues { get; set; } = null!;
    public DbSet<ToolboxTalk> ToolboxTalks { get; set; } = null!;
    public DbSet<ToolboxAttendee> ToolboxAttendees { get; set; } = null!;
    public DbSet<HseTrainingRecord> HseTrainingRecords { get; set; } = null!;
    public DbSet<StatutoryInspection> StatutoryInspections { get; set; } = null!;
    public DbSet<HseAuditLog> HseAuditLogs { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        if (DefaultSchema is not null)
            modelBuilder.HasDefaultSchema(DefaultSchema);
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        modelBuilder.Entity<Site>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<HseIncident>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<EnvIncident>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<CorrectiveAction>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Rams>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<PpeIssue>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<ToolboxTalk>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<ToolboxAttendee>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<HseTrainingRecord>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<StatutoryInspection>().HasQueryFilter(e => !e.IsDeleted);

        // EnvIncident is a 1:1 optional extension of HseIncident (HSE-007).
        modelBuilder.Entity<EnvIncident>()
            .HasOne(e => e.Incident)
            .WithOne(i => i.EnvIncident)
            .HasForeignKey<EnvIncident>(e => e.IncidentId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<CorrectiveAction>()
            .HasOne(c => c.Incident)
            .WithMany(i => i.CorrectiveActions)
            .HasForeignKey(c => c.IncidentId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ToolboxAttendee>()
            .HasOne(a => a.Talk)
            .WithMany(t => t.Attendees)
            .HasForeignKey(a => a.TalkId)
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
