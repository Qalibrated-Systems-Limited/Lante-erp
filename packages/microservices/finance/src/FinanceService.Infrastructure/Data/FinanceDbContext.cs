using FinanceService.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace FinanceService.Infrastructure.Data;

/// Finance GL backbone context. Base (public-schema) lineage; the tenant variant drops the schema.
public class FinanceDbContext : DbContext
{
    public FinanceDbContext(DbContextOptions options) : base(options) { }

    protected virtual string? DefaultSchema => "public";

    public DbSet<Currency> Currencies => Set<Currency>();
    public DbSet<ForexRevaluationLog> ForexRevaluationLogs => Set<ForexRevaluationLog>();
    public DbSet<FiscalYear> FiscalYears => Set<FiscalYear>();
    public DbSet<AccountingPeriod> AccountingPeriods => Set<AccountingPeriod>();
    public DbSet<PeriodCloseChecklistItem> PeriodCloseChecklistItems => Set<PeriodCloseChecklistItem>();
    public DbSet<PeriodCloseLog> PeriodCloseLogs => Set<PeriodCloseLog>();
    public DbSet<AccountType> AccountTypes => Set<AccountType>();
    public DbSet<ChartOfAccount> ChartOfAccounts => Set<ChartOfAccount>();
    public DbSet<CostCenter> CostCenters => Set<CostCenter>();
    public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();
    public DbSet<JournalLine> JournalLines => Set<JournalLine>();
    public DbSet<GeneralLedgerEntry> GeneralLedgerEntries => Set<GeneralLedgerEntry>();
    public DbSet<FinanceAuditLog> FinanceAuditLogs => Set<FinanceAuditLog>();

    // Accounts Receivable
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<TaxCategory> TaxCategories => Set<TaxCategory>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceLine> InvoiceLines => Set<InvoiceLine>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<PaymentAllocation> PaymentAllocations => Set<PaymentAllocation>();
    public DbSet<VatReturn> VatReturns => Set<VatReturn>();
    public DbSet<EtimsSubmission> EtimsSubmissions => Set<EtimsSubmission>();

    // Accounts Payable
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<SupplierInvoice> SupplierInvoices => Set<SupplierInvoice>();
    public DbSet<SupplierInvoiceLine> SupplierInvoiceLines => Set<SupplierInvoiceLine>();
    public DbSet<PaymentApprovalTier> PaymentApprovalTiers => Set<PaymentApprovalTier>();
    public DbSet<PaymentVoucher> PaymentVouchers => Set<PaymentVoucher>();
    public DbSet<PaymentApprovalLog> PaymentApprovalLogs => Set<PaymentApprovalLog>();

    // Budgeting
    public DbSet<Budget> Budgets => Set<Budget>();
    public DbSet<RevenueTarget> RevenueTargets => Set<RevenueTarget>();

    // Imprest (Process 14)
    public DbSet<ImprestRequest> ImprestRequests => Set<ImprestRequest>();
    public DbSet<ImprestRetirementLine> ImprestRetirementLines => Set<ImprestRetirementLine>();
    public DbSet<PersonalAdvance> PersonalAdvances => Set<PersonalAdvance>();

    // Bank reconciliation (Processes 9/10)
    public DbSet<BankReconciliation> BankReconciliations => Set<BankReconciliation>();
    public DbSet<BankStatementLine> BankStatementLines => Set<BankStatementLine>();

    // Statutory remittances (FIN-025)
    public DbSet<StatutoryRemittance> StatutoryRemittances => Set<StatutoryRemittance>();

    // Fixed Asset Register & Depreciation (ASSET-001..008)
    public DbSet<AssetCategory> AssetCategories => Set<AssetCategory>();
    public DbSet<FixedAsset> FixedAssets => Set<FixedAsset>();
    public DbSet<DepreciationEntry> DepreciationEntries => Set<DepreciationEntry>();
    public DbSet<AssetDisposal> AssetDisposals => Set<AssetDisposal>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
        if (DefaultSchema != null) b.HasDefaultSchema(DefaultSchema);

        // Decimal precision for money / rates.
        foreach (var prop in b.Model.GetEntityTypes()
                     .SelectMany(t => t.GetProperties())
                     .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
        {
            prop.SetColumnType(prop.Name.Contains("Rate") ? "numeric(18,6)" : "numeric(18,2)");
        }

        b.Entity<Customer>().HasIndex(x => x.Code).IsUnique();
        b.Entity<TaxCategory>().HasIndex(x => x.Code).IsUnique();
        b.Entity<Invoice>().HasIndex(x => x.InvoiceNo).IsUnique();
        b.Entity<Invoice>().HasIndex(x => new { x.CustomerId, x.Status });
        b.Entity<Payment>().HasIndex(x => x.PaymentNo).IsUnique();
        b.Entity<InvoiceLine>().HasIndex(x => x.InvoiceId);
        b.Entity<PaymentAllocation>().HasIndex(x => x.PaymentId);

        b.Entity<Supplier>().HasIndex(x => x.Code).IsUnique();
        b.Entity<SupplierInvoice>().HasIndex(x => x.InternalNo).IsUnique();
        b.Entity<PaymentVoucher>().HasIndex(x => x.VoucherNo).IsUnique();
        b.Entity<SupplierInvoiceLine>().HasIndex(x => x.SupplierInvoiceId);
        b.Entity<PaymentApprovalLog>().HasIndex(x => x.VoucherId);

        b.Entity<Budget>().HasIndex(x => x.FiscalYearId);
        b.Entity<RevenueTarget>().HasIndex(x => x.FiscalYearId);

        b.Entity<ImprestRequest>().HasIndex(x => x.RefNo).IsUnique();
        b.Entity<ImprestRequest>().HasIndex(x => x.Status);
        b.Entity<ImprestRetirementLine>().HasIndex(x => x.ImprestRequestId);
        b.Entity<PersonalAdvance>().HasIndex(x => x.ImprestRequestId);

        b.Entity<BankReconciliation>().HasIndex(x => x.RefNo).IsUnique();
        b.Entity<BankStatementLine>().HasIndex(x => x.ReconciliationId);

        b.Entity<StatutoryRemittance>().HasIndex(x => x.RefNo).IsUnique();
        b.Entity<StatutoryRemittance>().HasIndex(x => new { x.ObligationCode, x.Period }).IsUnique();

        b.Entity<FixedAsset>().HasIndex(x => x.AssetTag).IsUnique();
        b.Entity<FixedAsset>().HasIndex(x => x.Status);
        b.Entity<DepreciationEntry>().HasIndex(x => new { x.AssetId, x.Period }).IsUnique();
        b.Entity<AssetDisposal>().HasIndex(x => x.AssetId);

        // Uniqueness within a tenant schema.
        b.Entity<Currency>().HasIndex(x => x.Code).IsUnique();
        b.Entity<ChartOfAccount>().HasIndex(x => x.Code).IsUnique();
        b.Entity<CostCenter>().HasIndex(x => x.Code).IsUnique();
        b.Entity<JournalEntry>().HasIndex(x => x.EntryNo).IsUnique();
        b.Entity<AccountingPeriod>().HasIndex(x => x.Name);

        // Query indexes for reporting.
        b.Entity<GeneralLedgerEntry>().HasIndex(x => new { x.AccountId, x.EntryDate });
        b.Entity<GeneralLedgerEntry>().HasIndex(x => x.PeriodId);
        b.Entity<JournalLine>().HasIndex(x => x.JournalEntryId);
    }
}

/// Schema-per-tenant variant — schema-agnostic (search_path binds it at runtime). Own migration lineage.
public class TenantFinanceDbContext : FinanceDbContext
{
    public TenantFinanceDbContext(DbContextOptions<TenantFinanceDbContext> options) : base(options) { }
    protected override string? DefaultSchema => null;
}
