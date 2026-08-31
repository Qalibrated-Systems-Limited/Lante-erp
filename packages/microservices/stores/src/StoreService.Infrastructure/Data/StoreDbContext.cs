using System.Reflection;
using StoreService.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace StoreService.Infrastructure.Data;

public class StoreDbContext : DbContext
{
    public StoreDbContext(DbContextOptions<StoreDbContext> options) : base(options) { }

    /// <summary>Constructor for derived contexts (the schema-per-tenant variant).</summary>
    protected StoreDbContext(DbContextOptions options) : base(options) { }

    /// <summary>Null = schema-agnostic: the model emits unqualified names so the connection's
    /// search_path decides the schema (the tenant schema when the interceptor binds it, or the
    /// "stores" fallback for non-tenant requests).</summary>
    protected virtual string? DefaultSchema => null;

    public DbSet<Supplier> Suppliers { get; set; } = null!;
    public DbSet<ItemMaster> Items { get; set; } = null!;
    public DbSet<GoodsReceivedNote> GoodsReceivedNotes { get; set; } = null!;
    public DbSet<PurchasePriceHistory> PurchasePriceHistories { get; set; } = null!;
    public DbSet<StockUnit> StockUnits { get; set; } = null!;
    public DbSet<StoreIssueNote> StoreIssueNotes { get; set; } = null!;
    public DbSet<SoldItem> SoldItems { get; set; } = null!;
    public DbSet<StockTakeReconciliation> StockTakeReconciliations { get; set; } = null!;
    public DbSet<Category> Categories { get; set; } = null!;
    public DbSet<UnitOfMeasure> UnitsOfMeasure { get; set; } = null!;
    public DbSet<Location> Locations { get; set; } = null!;
    public DbSet<StockMovement> StockMovements { get; set; } = null!;
    public DbSet<StoreTransfer> StoreTransfers { get; set; } = null!;
    public DbSet<ItemPhoto> ItemPhotos { get; set; } = null!;
    public DbSet<GoodsRejectionNote> GoodsRejectionNotes { get; set; } = null!;
    public DbSet<StoreAuditLog> StoreAuditLogs { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        if (DefaultSchema is not null)
            modelBuilder.HasDefaultSchema(DefaultSchema);
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        // Global soft-delete filters
        modelBuilder.Entity<Supplier>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<ItemMaster>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<GoodsReceivedNote>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<GoodsRejectionNote>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<GoodsRejectionNote>().Property(x => x.RejectedQty).HasColumnType("numeric(18,2)");
        modelBuilder.Entity<PurchasePriceHistory>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<StockUnit>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<StoreIssueNote>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<SoldItem>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<StockTakeReconciliation>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Category>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<UnitOfMeasure>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<Location>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<StockMovement>().HasQueryFilter(e => !e.IsDeleted);
        modelBuilder.Entity<StoreTransfer>().HasQueryFilter(e => !e.IsDeleted);

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
