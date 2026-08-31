using StoreService.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace StoreService.Infrastructure.Data.Configurations;

public class ItemMasterConfiguration : IEntityTypeConfiguration<ItemMaster>
{
    public void Configure(EntityTypeBuilder<ItemMaster> builder)
    {
        builder.ToTable("item_masters");
        builder.HasKey(i => i.Id);

        builder.Property(i => i.ItemCode).IsRequired().HasMaxLength(100);
        builder.Property(i => i.Description).HasMaxLength(1000);
        builder.Property(i => i.MinSellingPrice).HasColumnType("numeric(18,2)");
        builder.Property(i => i.MinStockLevel).HasColumnType("numeric(18,2)");
        builder.Property(i => i.MaxStockLevel).HasColumnType("numeric(18,2)");
        builder.Property(i => i.ReorderQty).HasColumnType("numeric(18,2)");
        builder.Property(i => i.AvgWeightedCost).HasColumnType("numeric(18,4)");
        builder.Property(i => i.QtyOnHand).HasColumnType("numeric(18,2)");
        builder.Property(i => i.LowStockAcknowledgedBy).HasMaxLength(255);

        builder.HasOne(i => i.Supplier)
            .WithMany(s => s.Items)
            .HasForeignKey(i => i.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.Category)
            .WithMany(c => c.Items)
            .HasForeignKey(i => i.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(i => i.UnitOfMeasure)
            .WithMany(u => u.Items)
            .HasForeignKey(i => i.UomId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(i => i.ItemCode).IsUnique();
        builder.HasIndex(i => i.CategoryId);
        builder.HasIndex(i => i.SupplierId);
        builder.HasIndex(i => i.UomId);
    }
}
