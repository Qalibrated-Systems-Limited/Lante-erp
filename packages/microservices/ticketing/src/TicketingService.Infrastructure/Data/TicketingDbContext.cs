using System.Linq;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using TicketingService.Core.Entities;

namespace TicketingService.Infrastructure.Data;

public class TicketingDbContext : DbContext
{
    public TicketingDbContext(DbContextOptions<TicketingDbContext> options) : base(options) { }

    /// <summary>Constructor for derived contexts (e.g. the schema-per-tenant variant).</summary>
    protected TicketingDbContext(DbContextOptions options) : base(options) { }

    /// <summary>
    /// Default Postgres schema. Null = schema-agnostic: the model emits UNQUALIFIED table names so
    /// the connection's search_path decides the schema (public for public-resident tenants, or the
    /// tenant schema when the interceptor binds it). Previously "public", which qualified every query
    /// as public.* and defeated search_path routing.
    /// </summary>
    protected virtual string? DefaultSchema => null;

    public DbSet<Ticket> Tickets { get; set; } = null!;
    public DbSet<Customer> Customers { get; set; } = null!;
    public DbSet<TicketCategory> TicketCategories { get; set; } = null!;
    public DbSet<SLAPolicy> SLAPolicies { get; set; } = null!;
    public DbSet<EscalationRule> EscalationRules { get; set; } = null!;
    public DbSet<TicketAssignment> TicketAssignments { get; set; } = null!;
    public DbSet<TicketComment> TicketComments { get; set; } = null!;
    public DbSet<TicketAttachment> TicketAttachments { get; set; } = null!;
    public DbSet<TicketHistory> TicketHistories { get; set; } = null!;
    public DbSet<TicketEscalation> TicketEscalations { get; set; } = null!;
    public DbSet<TicketWatcher> TicketWatchers { get; set; } = null!;
    public DbSet<Notification> Notifications { get; set; } = null!;
    public DbSet<Alert> Alerts { get; set; } = null!;
    public DbSet<Tag> Tags { get; set; } = null!;
    public DbSet<TicketTag> TicketTags { get; set; } = null!;
    public DbSet<Macro> Macros { get; set; } = null!;
    public DbSet<WorkflowRule> WorkflowRules { get; set; } = null!;
    public DbSet<TicketSatisfactionRating> SatisfactionRatings { get; set; } = null!;
    public DbSet<ComplaintWorkflowStep> ComplaintWorkflowSteps { get; set; } = null!;
    public DbSet<KnowledgeBaseArticle> KnowledgeBaseArticles { get; set; } = null!;

    // Service & Calibration Requests
    public DbSet<PendingVerification> PendingVerifications { get; set; } = null!;
    public DbSet<ServiceRequest> ServiceRequests { get; set; } = null!;
    public DbSet<Quotation> Quotations { get; set; } = null!;

    public DbSet<TicketingAuditLog> TicketingAuditLogs { get; set; } = null!;

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

        // Soft-delete filters — TicketHistory is NEVER deleted
        modelBuilder.Entity<Ticket>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Customer>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<TicketCategory>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<SLAPolicy>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<EscalationRule>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<TicketAssignment>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<TicketComment>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<TicketAttachment>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<TicketEscalation>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<TicketWatcher>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Notification>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Alert>().HasQueryFilter(e => !e.IsDeleted);
        // Alerts are a shared platform-wide table (public schema, TenantId column), but Tickets are
        // schema-per-tenant — so Alert.TicketId must stay a plain unconstrained string, never an FK
        // into public.Tickets. Ignore the navigation so convention discovery can't recreate the
        // (already dropped, see 20260709135512_DropAlertsTicketFK) FK_Alerts_Tickets_TicketId.
        modelBuilder.Entity<Alert>().Ignore(a => a.Ticket);

        // Closes the dedup race in AlertService.CreateAsync, which does ExistsOpenAsync() then
        // AddAsync() with nothing underneath — classic check-then-act. Two replicas both see "no open
        // alert" and both insert, so the same SLA breach raises two alerts and notifies twice (#219).
        //
        // PARTIAL, matching ExistsOpenAsync's predicate exactly: (TenantId, Source, Title) while the
        // alert is neither acknowledged nor deleted. A plain unique index would be wrong — once an alert
        // is acknowledged the same condition SHOULD be able to raise a fresh one, which is the whole
        // point of acknowledging it. The filter is where that distinction lives.
        modelBuilder.Entity<Alert>()
            .HasIndex(a => new { a.TenantId, a.Source, a.Title })
            .IsUnique()
            .HasFilter("NOT \"IsAcknowledged\" AND NOT \"IsDeleted\"");
        modelBuilder.Entity<Tag>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<TicketTag>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Macro>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<WorkflowRule>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<TicketSatisfactionRating>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<ComplaintWorkflowStep>().HasQueryFilter(e => !e.IsDeleted);   // D5
        modelBuilder.Entity<KnowledgeBaseArticle>().HasQueryFilter(e => !e.IsDeleted);    // D7
        // TicketHistory intentionally excluded — append-only, never deleted

        // Service & Calibration Requests
        modelBuilder.Entity<PendingVerification>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<ServiceRequest>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Quotation>().HasQueryFilter(e => !e.IsDeleted);

        // ServiceRequest → Quotation (1:0..1 via QuotationId FK on ServiceRequest)
        modelBuilder.Entity<Quotation>()
            .HasOne(q => q.ServiceRequest)
            .WithOne(r => r.Quotation)
            .HasForeignKey<Quotation>(q => q.ServiceRequestId)
            .OnDelete(DeleteBehavior.Cascade);

        // D1-2 — Ticket → Customer (many:0..1); keep tickets if a customer is soft/hard-deleted.
        modelBuilder.Entity<Ticket>()
            .HasOne(t => t.Customer)
            .WithMany(c => c.Tickets)
            .HasForeignKey(t => t.CustomerId)
            .OnDelete(DeleteBehavior.SetNull);

        // D1-4 — per-tenant sequential ticket number. Numbers repeat across tenants (the
        // shared/public plane is multi-tenant), so the constraint is scoped to (TenantId,
        // TicketNumber), not TicketNumber alone. Unique, not just indexed: MAX+1 assignment
        // (TicketRepository.GetNextTicketNumberAsync) is racy under concurrent creates for the same
        // tenant — this index is the real guard; TicketService.CreateAsync retries on collision.
        // Reference is a computed (unmapped) property.
        modelBuilder.Entity<Ticket>().Ignore(t => t.Reference);
        modelBuilder.Entity<Ticket>()
            .HasIndex(t => new { t.TenantId, t.TicketNumber }).IsUnique();

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
