using System.Linq;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using ComplianceService.Core.Entities;

namespace ComplianceService.Infrastructure.Data;

public class ComplianceDbContext : DbContext
{
    public ComplianceDbContext(DbContextOptions<ComplianceDbContext> options) : base(options) { }

    /// <summary>Constructor for derived contexts (the schema-per-tenant variant).</summary>
    protected ComplianceDbContext(DbContextOptions options) : base(options) { }

    /// <summary>
    /// Null = schema-agnostic: unqualified table names, so the connection's search_path (set by
    /// TenantDbConnectionInterceptor) decides which tenant schema is queried. Same convention as
    /// TicketingDbContext / HSEDbContext.
    /// </summary>
    protected virtual string? DefaultSchema => null;

    public DbSet<GiftHospitality> GiftHospitalities { get; set; } = null!;
    public DbSet<CoiDeclaration> CoiDeclarations { get; set; } = null!;
    public DbSet<WhistleblowerCase> WhistleblowerCases { get; set; } = null!;
    public DbSet<DataSubjectRequest> DataSubjectRequests { get; set; } = null!;
    public DbSet<DataBreach> DataBreaches { get; set; } = null!;
    public DbSet<CaseAction> CaseActions { get; set; } = null!;
    public DbSet<Policy> Policies { get; set; } = null!;
    public DbSet<PolicyAck> PolicyAcks { get; set; } = null!;
    public DbSet<BoardResolution> BoardResolutions { get; set; } = null!;
    public DbSet<RegulatoryLicence> RegulatoryLicences { get; set; } = null!;
    public DbSet<AntiBriberyTraining> AntiBriberyTrainings { get; set; } = null!;
    public DbSet<RelatedPartyTransaction> RelatedPartyTransactions { get; set; } = null!;
    public DbSet<StatutoryObligation> StatutoryObligations { get; set; } = null!;
    public DbSet<StatutoryDeadline> StatutoryDeadlines { get; set; } = null!;
    public DbSet<AnnualReturn> AnnualReturns { get; set; } = null!;
    public DbSet<TaxComplianceCert> TaxComplianceCerts { get; set; } = null!;
    public DbSet<CosecTask> CosecTasks { get; set; } = null!;
    public DbSet<RelatedParty> RelatedParties { get; set; } = null!;
    public DbSet<Icsa> Icsas { get; set; } = null!;
    public DbSet<IntercompanyTxn> IntercompanyTxns { get; set; } = null!;
    public DbSet<CustomerSurveyResponse> CustomerSurveyResponses { get; set; } = null!;
    public DbSet<SopDocument> SopDocuments { get; set; } = null!;
    public DbSet<ComplianceAuditLog> ComplianceAuditLogs { get; set; } = null!;

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

        modelBuilder.Entity<GiftHospitality>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<CoiDeclaration>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<WhistleblowerCase>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<DataSubjectRequest>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<DataBreach>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<CaseAction>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Policy>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<PolicyAck>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<BoardResolution>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<RegulatoryLicence>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<AntiBriberyTraining>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<RelatedPartyTransaction>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<StatutoryObligation>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<StatutoryDeadline>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<AnnualReturn>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<TaxComplianceCert>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<CosecTask>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<RelatedParty>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Icsa>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<IntercompanyTxn>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<CustomerSurveyResponse>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<SopDocument>().HasQueryFilter(e => !e.IsDeleted);

        // Belt-and-suspenders alongside SopLibraryService's transactional count+insert —
        // guarantees the DB itself never accepts a duplicate Code even under a bug/race.
        // Filtered to non-deleted rows so a soft-deleted SOP's old code can't block a
        // legitimately-renumbered new one.
        modelBuilder.Entity<SopDocument>().HasIndex(s => s.Code).IsUnique().HasFilter("\"IsDeleted\" = false");

        modelBuilder.Entity<PolicyAck>()
            .HasOne(a => a.Policy)
            .WithMany(p => p.Acknowledgements)
            .HasForeignKey(a => a.PolicyId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<StatutoryDeadline>()
            .HasOne(d => d.Obligation)
            .WithMany(o => o.Deadlines)
            .HasForeignKey(d => d.ObligationId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Icsa>()
            .HasOne(a => a.RelatedParty)
            .WithMany()
            .HasForeignKey(a => a.RelatedPartyId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<IntercompanyTxn>()
            .HasOne(t => t.Icsa)
            .WithMany()
            .HasForeignKey(t => t.IcsaId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<IntercompanyTxn>()
            .HasOne(t => t.RelatedParty)
            .WithMany()
            .HasForeignKey(t => t.RelatedPartyId)
            .OnDelete(DeleteBehavior.Restrict);

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
