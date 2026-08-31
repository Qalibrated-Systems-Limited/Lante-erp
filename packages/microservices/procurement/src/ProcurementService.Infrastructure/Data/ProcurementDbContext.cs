using Microsoft.EntityFrameworkCore;
using ProcurementService.Core.Entities;
using System.Linq;

namespace ProcurementService.Infrastructure.Data;

/// <summary>
/// Procurement &amp; Supply Chain module context (Module 4). Schema-per-tenant: the base model is
/// schema-agnostic; the tenant schema is bound via the connection's Postgres search_path (see
/// <see cref="TenantDbConnectionInterceptor"/> at request time, and TenantProvisioningService for
/// migration). DbSets are added per phase (P1 ASR onward).
/// </summary>
public class ProcurementDbContext : DbContext
{
    public ProcurementDbContext(DbContextOptions<ProcurementDbContext> options) : base(options) { }

    /// <summary>Constructor for derived contexts (e.g. the schema-per-tenant variant).</summary>
    protected ProcurementDbContext(DbContextOptions options) : base(options) { }

    // ── P1: Approved Supplier Register ──
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<SupplierCategory> SupplierCategories => Set<SupplierCategory>();
    public DbSet<SupplierDocument> SupplierDocuments => Set<SupplierDocument>();
    public DbSet<GiftRegister> GiftRegisters => Set<GiftRegister>();
    public DbSet<ProcurementAuditLog> AuditLogs => Set<ProcurementAuditLog>();

    // ── P2: Purchase Requisition ──
    public DbSet<PurchaseRequisition> PurchaseRequisitions => Set<PurchaseRequisition>();
    public DbSet<PurchaseRequisitionLine> PurchaseRequisitionLines => Set<PurchaseRequisitionLine>();
    public DbSet<PrApprovalLog> PrApprovalLogs => Set<PrApprovalLog>();

    // ── P3: Quotation & comparative analysis ──
    public DbSet<Quotation> Quotations => Set<Quotation>();
    public DbSet<QuotationLine> QuotationLines => Set<QuotationLine>();
    public DbSet<QuotationComparison> QuotationComparisons => Set<QuotationComparison>();

    // ── P4: LPO / Purchase Order ──
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<PoApproval> PoApprovals => Set<PoApproval>();

    // ── P6: 3-way match & payment voucher ──
    public DbSet<ThreeWayMatch> ThreeWayMatches => Set<ThreeWayMatch>();
    public DbSet<MatchingException> MatchingExceptions => Set<MatchingException>();

    // ── P7: international sourcing ──
    public DbSet<InternationalPo> InternationalPos => Set<InternationalPo>();
    public DbSet<LandedCostComponent> LandedCostComponents => Set<LandedCostComponent>();
    public DbSet<CustomsDeclaration> CustomsDeclarations => Set<CustomsDeclaration>();

    // ── P8: emergency procurement ──
    public DbSet<EmergencyProcurementLog> EmergencyProcurementLogs => Set<EmergencyProcurementLog>();

    // ── P9: supplier performance review ──
    public DbSet<SupplierPerformanceReview> SupplierPerformanceReviews => Set<SupplierPerformanceReview>();

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

        modelBuilder.Entity<Supplier>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            e.HasIndex(x => x.SupplierNumber).IsUnique();
            e.HasIndex(x => x.Name);
            e.HasIndex(x => x.KraPin);
            e.HasIndex(x => x.Status);
            e.HasIndex(x => new { x.IsApproved, x.BlacklistFlag });
            e.HasIndex(x => x.CategoryId);
            e.Property(x => x.OverallScore).HasColumnType("decimal(5,2)");
            e.HasMany(x => x.Documents).WithOne(d => d.Supplier!).HasForeignKey(d => d.SupplierId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SupplierCategory>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            e.HasIndex(x => x.CategoryName);
            e.Property(x => x.MinScoreThreshold).HasColumnType("decimal(5,2)");
        });

        modelBuilder.Entity<SupplierDocument>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            e.HasIndex(x => x.SupplierId);
            e.HasIndex(x => x.ExpiryDate);
        });

        modelBuilder.Entity<GiftRegister>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            e.HasIndex(x => x.SupplierId);
            e.HasIndex(x => x.ReceivedBy);
            e.HasIndex(x => x.DeclaredAt);
            e.Property(x => x.EstimatedValue).HasColumnType("decimal(18,2)");
        });

        modelBuilder.Entity<ProcurementAuditLog>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            e.HasIndex(x => new { x.EntityType, x.EntityId });
            e.HasIndex(x => x.OccurredAt);
        });

        modelBuilder.Entity<PurchaseRequisition>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            e.HasIndex(x => x.PrNumber).IsUnique();
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.DepartmentId);
            e.HasIndex(x => x.RequestedBy);
            e.Property(x => x.TotalEstimated).HasColumnType("decimal(18,2)");
            e.HasMany(x => x.Lines).WithOne(l => l.Pr!).HasForeignKey(l => l.PrId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PurchaseRequisitionLine>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            e.HasIndex(x => x.PrId);
            foreach (var p in new[] { "Quantity", "EstimatedUnitPrice", "LineTotal" })
                e.Property(p).HasColumnType("decimal(18,2)");
        });

        modelBuilder.Entity<PrApprovalLog>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            e.HasIndex(x => x.PrId);
        });

        modelBuilder.Entity<Quotation>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            e.HasIndex(x => x.QuoteNumber).IsUnique();
            e.HasIndex(x => x.PrId);
            e.HasIndex(x => x.SupplierId);
            foreach (var p in new[] { "TotalQuoted", "PriceScore", "QualityScore", "DeliveryScore", "TotalScore" })
                e.Property(p).HasColumnType("decimal(18,2)");
            e.HasMany(x => x.Lines).WithOne(l => l.Quotation!).HasForeignKey(l => l.QuotationId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<QuotationLine>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            e.HasIndex(x => x.QuotationId);
            foreach (var p in new[] { "Quantity", "UnitPrice", "LineTotal" })
                e.Property(p).HasColumnType("decimal(18,2)");
        });

        modelBuilder.Entity<QuotationComparison>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            e.HasIndex(x => x.PrId).IsUnique();
            foreach (var p in new[] { "PriceScore", "QualityScore", "DeliveryScore", "OverallScore" })
                e.Property(p).HasColumnType("decimal(18,2)");
        });

        modelBuilder.Entity<PurchaseOrder>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            e.HasIndex(x => x.PoNumber).IsUnique();
            e.HasIndex(x => x.PrId);
            e.HasIndex(x => x.SupplierId);
            e.HasIndex(x => x.Status);
            e.Property(x => x.TotalAmount).HasColumnType("decimal(18,2)");
            e.HasMany(x => x.Approvals).WithOne(a => a.Po!).HasForeignKey(a => a.PoId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PoApproval>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            e.HasIndex(x => x.PoId);
        });

        modelBuilder.Entity<ThreeWayMatch>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            e.HasIndex(x => x.PoId).IsUnique();
            e.HasIndex(x => x.Status);
            foreach (var p in new[] { "PoTotal", "InvoiceTotal", "ReceivedQty" })
                e.Property(p).HasColumnType("decimal(18,2)");
        });

        modelBuilder.Entity<MatchingException>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            e.HasIndex(x => x.PoId);
            e.HasIndex(x => x.Status);
        });

        modelBuilder.Entity<InternationalPo>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            e.HasIndex(x => x.PoId).IsUnique();      // 1:1 with the LPO
            e.HasIndex(x => x.Status);
            foreach (var p in new[] { "PurchasePriceFx", "PurchasePriceKes", "TotalLandedCostKes", "LandedCostPerUnitKes", "QuantityBasis", "TtAmountFx" })
                e.Property(p).HasColumnType("decimal(18,2)");
            e.Property(x => x.ExchangeRateAtOrder).HasColumnType("decimal(18,6)");
            e.HasMany(x => x.Components).WithOne(c => c.IntlPo!).HasForeignKey(c => c.IntlPoId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<LandedCostComponent>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            e.HasIndex(x => x.IntlPoId);
            e.Property(x => x.AmountFx).HasColumnType("decimal(18,2)");
            e.Property(x => x.KesAmount).HasColumnType("decimal(18,2)");
            e.Property(x => x.ExchangeRate).HasColumnType("decimal(18,6)");
        });

        modelBuilder.Entity<CustomsDeclaration>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            e.HasIndex(x => x.IntlPoId).IsUnique();   // one declaration per order
            foreach (var p in new[] { "ImportDutyKes", "ClearingAgentFeeKes", "PortChargesKes" })
                e.Property(p).HasColumnType("decimal(18,2)");
        });

        modelBuilder.Entity<EmergencyProcurementLog>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            e.HasIndex(x => x.PoId).IsUnique();       // one emergency declaration per LPO
            e.HasIndex(x => x.BoardPackPeriod);       // monthly board-pack reporting
            e.HasIndex(x => x.DeclaredAt);
        });

        modelBuilder.Entity<SupplierPerformanceReview>(e =>
        {
            e.HasQueryFilter(x => !x.IsDeleted);
            e.HasIndex(x => new { x.SupplierId, x.ReviewPeriod }).IsUnique();   // one review per supplier per half
            e.HasIndex(x => x.ReviewPeriod);
            e.HasIndex(x => x.Outcome);
            foreach (var p in new[] { "QualityScore", "DeliveryScore", "PricingScore", "ComplianceScore",
                                      "OverallScore", "AssessedWeight", "AcceptedQty", "RejectedQty",
                                      "RejectRatePct", "AvgLeadTimeDays", "CategoryMinScore" })
                e.Property(p).HasColumnType("decimal(18,2)");
        });
    }
}
