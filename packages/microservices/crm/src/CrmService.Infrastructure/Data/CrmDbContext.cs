using Microsoft.EntityFrameworkCore;
using CrmService.Core.Entities;
using System.Linq;

namespace CrmService.Infrastructure.Data;

/// <summary>
/// CRM &amp; Sales module context (Module 6). Schema-per-tenant: the base model is schema-agnostic;
/// the tenant schema is bound via the connection's Postgres search_path (see
/// <see cref="TenantDbConnectionInterceptor"/> at request time, and TenantProvisioningService for
/// migration). DbSets are added per phase (C1 CUSTOMER onward).
/// </summary>
public class CrmDbContext : DbContext
{
    public CrmDbContext(DbContextOptions<CrmDbContext> options) : base(options) { }

    /// <summary>Constructor for derived contexts (e.g. the schema-per-tenant variant).</summary>
    protected CrmDbContext(DbContextOptions options) : base(options) { }

    // ── C1: Customer master ──
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<CustomerContact> CustomerContacts => Set<CustomerContact>();
    public DbSet<ClientCreditLimit> ClientCreditLimits => Set<ClientCreditLimit>();

    // ── C2: Leads ──
    public DbSet<Lead> Leads => Set<Lead>();
    public DbSet<LeadActivity> LeadActivities => Set<LeadActivity>();

    // ── C3: Opportunities & pipeline ──
    public DbSet<PipelineStage> PipelineStages => Set<PipelineStage>();
    public DbSet<Opportunity> Opportunities => Set<Opportunity>();
    public DbSet<OpportunityActivity> OpportunityActivities => Set<OpportunityActivity>();
    public DbSet<OpportunityCompetitor> OpportunityCompetitors => Set<OpportunityCompetitor>();

    // ── C4: Quotations ──
    public DbSet<Quotation> Quotations => Set<Quotation>();
    public DbSet<QuotationLine> QuotationLines => Set<QuotationLine>();
    public DbSet<PriceList> PriceLists => Set<PriceList>();
    public DbSet<PriceExceptionLog> PriceExceptionLogs => Set<PriceExceptionLog>();

    // ── C5: Deals & contracts ──
    public DbSet<Deal> Deals => Set<Deal>();
    public DbSet<DealProduct> DealProducts => Set<DealProduct>();
    public DbSet<Contract> Contracts => Set<Contract>();

    // ── C6: Tenders & bid bonds ──
    public DbSet<Tender> Tenders => Set<Tender>();
    public DbSet<TenderBidBond> TenderBidBonds => Set<TenderBidBond>();

    // ── C7: Client interaction & activity ──
    public DbSet<CustomerInteraction> CustomerInteractions => Set<CustomerInteraction>();
    public DbSet<ActivityTask> ActivityTasks => Set<ActivityTask>();
    public DbSet<ClientVisit> ClientVisits => Set<ClientVisit>();
    public DbSet<VisitTarget> VisitTargets => Set<VisitTarget>();
    public DbSet<SalesActivityLog> SalesActivityLogs => Set<SalesActivityLog>();

    // ── C8: Account ownership transfer ──
    public DbSet<ClientTransferRequest> ClientTransferRequests => Set<ClientTransferRequest>();
    public DbSet<ClientTransferHandover> ClientTransferHandovers => Set<ClientTransferHandover>();

    // ── C9: Performance & pipeline snapshots ──
    public DbSet<SalesTarget> SalesTargets => Set<SalesTarget>();
    public DbSet<RevenueSnapshot> RevenueSnapshots => Set<RevenueSnapshot>();
    public DbSet<PipelineSnapshot> PipelineSnapshots => Set<PipelineSnapshot>();

    // ── C10: Marketing ──
    public DbSet<Campaign> Campaigns => Set<Campaign>();
    public DbSet<BrandAsset> BrandAssets => Set<BrandAsset>();

    // ── C11: After-sales & retention ──
    public DbSet<ClientSatisfactionSurvey> ClientSatisfactionSurveys => Set<ClientSatisfactionSurvey>();
    public DbSet<ServiceContract> ServiceContracts => Set<ServiceContract>();
    public DbSet<ClientComplaint> ClientComplaints => Set<ClientComplaint>();
    public DbSet<NpsSurvey> NpsSurveys => Set<NpsSurvey>();

    // ── C12: Legal & contract register ──
    public DbSet<NdaRegister> NdaRegisters => Set<NdaRegister>();
    public DbSet<FrameworkAgreement> FrameworkAgreements => Set<FrameworkAgreement>();
    public DbSet<SubcontractorAgreement> SubcontractorAgreements => Set<SubcontractorAgreement>();
    public DbSet<CarrierAgreement> CarrierAgreements => Set<CarrierAgreement>();

    // ── C13: Payment / debtor alerts (log only — reads Finance for the data) ──
    public DbSet<PaymentAlertLog> PaymentAlertLogs => Set<PaymentAlertLog>();

    // ── #216: field-level audit trail, written by CrmAuditInterceptor ──
    public DbSet<CrmAuditLog> CrmAuditLogs => Set<CrmAuditLog>();

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
                : "numeric(18,2)");
        }

        modelBuilder.Entity<Customer>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            e.HasIndex(x => x.Name);
            e.HasIndex(x => x.Email);
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.AccountOwnerId);
            e.Property(x => x.CreditLimit).HasColumnType("decimal(18,2)");
            e.HasMany(x => x.Contacts).WithOne(c => c.Customer!).HasForeignKey(c => c.CustomerId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CustomerContact>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            e.HasIndex(x => x.CustomerId);
        });

        modelBuilder.Entity<ClientCreditLimit>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            e.HasIndex(x => x.CustomerId);
            e.Property(x => x.CreditLimit).HasColumnType("decimal(18,2)");
        });

        modelBuilder.Entity<Lead>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.AssignedTo);
            e.HasIndex(x => x.CampaignId);
            e.Property(x => x.EstimatedValue).HasColumnType("decimal(18,2)");
            e.HasMany(x => x.Activities).WithOne(a => a.Lead!).HasForeignKey(a => a.LeadId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<LeadActivity>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            e.HasIndex(x => x.LeadId);
        });

        modelBuilder.Entity<PipelineStage>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            e.Property(x => x.WinProbability).HasColumnType("decimal(4,2)");
        });

        modelBuilder.Entity<Opportunity>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.PipelineStageId);
            e.HasIndex(x => x.AssignedTo);
            e.HasIndex(x => x.OpportunityNumber).IsUnique();
            e.Property(x => x.EstimatedValue).HasColumnType("decimal(18,2)");
            e.Property(x => x.Probability).HasColumnType("decimal(4,2)");
            e.HasMany(x => x.Activities).WithOne(a => a.Opportunity!).HasForeignKey(a => a.OpportunityId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Competitors).WithOne(a => a.Opportunity!).HasForeignKey(a => a.OpportunityId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<OpportunityActivity>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            e.HasIndex(x => x.OpportunityId);
        });

        modelBuilder.Entity<OpportunityCompetitor>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            e.HasIndex(x => x.OpportunityId);
        });

        modelBuilder.Entity<Quotation>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            e.HasIndex(x => x.OpportunityId);
            e.HasIndex(x => x.CustomerId);
            e.HasIndex(x => x.Status);
            // Unique, not just indexed: QuoteNumber is minted by counting existing rows
            // (QuotationService.GenerateNumberAsync) — this constraint is the actual guard against
            // two concurrent CreateAsync calls both computing the same next number and both
            // committing a Version=1 row for it. ReviseAsync's Version = src.Version + 1 keeps this
            // constraint satisfied for legitimate revisions.
            e.HasIndex(x => new { x.QuoteNumber, x.Version }).IsUnique();
            foreach (var p in new[] { "Subtotal", "DiscountAmount", "VatRate", "VatAmount", "TotalAmount" })
                e.Property(p).HasColumnType("decimal(18,2)");
            e.HasMany(x => x.Lines).WithOne(l => l.Quotation!).HasForeignKey(l => l.QuotationId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<QuotationLine>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            e.HasIndex(x => x.QuotationId);
            foreach (var p in new[] { "Quantity", "UnitPrice", "DiscountPercent", "LineTotal" })
                e.Property(p).HasColumnType("decimal(18,2)");
        });

        modelBuilder.Entity<PriceList>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            e.Property(x => x.StandardRate).HasColumnType("decimal(18,2)");
            e.Property(x => x.DiscountLimitPercent).HasColumnType("decimal(5,2)");
        });

        modelBuilder.Entity<PriceExceptionLog>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            e.HasIndex(x => x.QuotationId);
            foreach (var p in new[] { "LastSalePrice", "ProposedPrice", "VariancePct" })
                e.Property(p).HasColumnType("decimal(18,2)");
        });

        modelBuilder.Entity<Deal>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            e.HasIndex(x => x.OpportunityId);
            e.HasIndex(x => x.CustomerId);
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.DealNumber).IsUnique();
            e.Property(x => x.ContractValue).HasColumnType("decimal(18,2)");
            e.HasMany(x => x.Products).WithOne(p => p.Deal!).HasForeignKey(p => p.DealId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DealProduct>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            e.HasIndex(x => x.DealId);
            foreach (var p in new[] { "Quantity", "UnitPrice", "TotalPrice" })
                e.Property(p).HasColumnType("decimal(18,2)");
        });

        modelBuilder.Entity<Contract>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            e.HasIndex(x => x.DealId);
            e.HasIndex(x => x.EndDate);
            e.HasIndex(x => x.ContractNumber).IsUnique();
            e.Property(x => x.Value).HasColumnType("decimal(18,2)");
            e.Property(x => x.RetentionPct).HasColumnType("decimal(5,2)");
        });

        modelBuilder.Entity<Tender>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.SubmissionDeadline);
            e.HasIndex(x => x.TenderNumber).IsUnique();
            e.Property(x => x.EstimatedValue).HasColumnType("decimal(18,2)");
            e.HasOne(x => x.BidBond).WithOne(b => b.Tender!).HasForeignKey<TenderBidBond>(b => b.TenderId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TenderBidBond>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            e.HasIndex(x => x.TenderId).IsUnique();
            e.HasIndex(x => x.ValidityDate);
            e.Property(x => x.Amount).HasColumnType("decimal(18,2)");
        });

        modelBuilder.Entity<CustomerInteraction>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            e.HasIndex(x => x.CustomerId);
            e.HasIndex(x => x.InteractionDate);
        });
        modelBuilder.Entity<ActivityTask>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            e.HasIndex(x => x.AssignedTo);
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.DueDate);
            e.HasIndex(x => x.CustomerId);
        });
        modelBuilder.Entity<ClientVisit>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            e.HasIndex(x => x.CustomerId);
            e.HasIndex(x => x.EmployeeId);
        });
        modelBuilder.Entity<VisitTarget>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            e.HasIndex(x => x.EmployeeId).IsUnique();
        });
        modelBuilder.Entity<SalesActivityLog>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            e.HasIndex(x => new { x.EmployeeId, x.LogDate });
        });

        modelBuilder.Entity<ClientTransferRequest>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            e.HasIndex(x => x.CustomerId);
            e.HasIndex(x => x.Status);
            e.HasOne(x => x.Handover).WithOne(h => h.TransferRequest!).HasForeignKey<ClientTransferHandover>(h => h.TransferRequestId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<ClientTransferHandover>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            e.HasIndex(x => x.TransferRequestId).IsUnique();
        });

        modelBuilder.Entity<SalesTarget>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            e.HasIndex(x => new { x.EmployeeId, x.PeriodType, x.PeriodLabel });
            e.Property(x => x.RevenueTarget).HasColumnType("decimal(18,2)");
        });
        modelBuilder.Entity<RevenueSnapshot>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            e.HasIndex(x => new { x.EmployeeId, x.SnapshotDate });
            foreach (var p in new[] { "DailyRevenue", "WtdRevenue", "MtdRevenue", "YtdRevenue", "TargetMtd" })
                e.Property(p).HasColumnType("decimal(18,2)");
        });
        modelBuilder.Entity<PipelineSnapshot>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            e.HasIndex(x => x.SnapshotDate);
            e.Property(x => x.TotalPipelineValue).HasColumnType("decimal(18,2)");
            e.Property(x => x.WeightedPipelineValue).HasColumnType("decimal(18,2)");
        });

        modelBuilder.Entity<Campaign>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            e.HasIndex(x => x.Status);
            e.Property(x => x.Budget).HasColumnType("decimal(18,2)");
            e.Property(x => x.ActualSpend).HasColumnType("decimal(18,2)");
        });
        modelBuilder.Entity<BrandAsset>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            e.HasIndex(x => x.AssetType);
        });

        modelBuilder.Entity<ClientSatisfactionSurvey>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            e.HasIndex(x => x.CustomerId);
            e.HasIndex(x => x.Status);
        });
        modelBuilder.Entity<ServiceContract>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            e.HasIndex(x => x.CustomerId);
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.EndDate);
            e.HasIndex(x => x.ContractNumber).IsUnique();
            e.Property(x => x.Value).HasColumnType("decimal(18,2)");
        });
        modelBuilder.Entity<ClientComplaint>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            e.HasIndex(x => x.CustomerId);
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.ComplaintNumber).IsUnique();
        });
        modelBuilder.Entity<NpsSurvey>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            e.HasIndex(x => new { x.CustomerId, x.Year });
            e.HasIndex(x => x.Status);
        });

        modelBuilder.Entity<Customer>().Property(x => x.LastSatisfactionScore).HasColumnType("decimal(4,2)");

        modelBuilder.Entity<NdaRegister>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.ExpiryDate);
            e.HasIndex(x => x.NdaNumber).IsUnique();
        });
        modelBuilder.Entity<FrameworkAgreement>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.EndDate);
            e.HasIndex(x => x.AgreementNumber).IsUnique();
            e.Property(x => x.Value).HasColumnType("decimal(18,2)");
        });
        modelBuilder.Entity<SubcontractorAgreement>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.EndDate);
            e.HasIndex(x => x.AgreementNumber).IsUnique();
            e.Property(x => x.Value).HasColumnType("decimal(18,2)");
        });
        modelBuilder.Entity<CarrierAgreement>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            e.HasIndex(x => x.VettingStatus);
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.AgreementNumber).IsUnique();
        });

        modelBuilder.Entity<PaymentAlertLog>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.AlertType);
            e.HasIndex(x => x.DedupKey).IsUnique();
            e.Property(x => x.Amount).HasColumnType("decimal(18,2)");
        });
    }
}
