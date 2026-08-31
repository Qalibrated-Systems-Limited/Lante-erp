using StoreService.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace StoreService.Infrastructure.Data.Configurations;

public class SoldItemConfiguration : IEntityTypeConfiguration<SoldItem>
{
    public void Configure(EntityTypeBuilder<SoldItem> builder)
    {
        builder.ToTable("sold_items");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.ClientId).IsRequired().HasMaxLength(100);
        builder.Property(s => s.SerialNo).HasMaxLength(255);
        builder.Property(s => s.Qty).HasColumnType("numeric(18,2)");
        builder.Property(s => s.SalePrice).HasColumnType("numeric(18,2)");
        builder.Property(s => s.InvoiceNo).IsRequired().HasMaxLength(100);
        builder.Property(s => s.CostAtSale).HasColumnType("numeric(18,4)");

        builder.HasOne(s => s.Item)
            .WithMany()
            .HasForeignKey(s => s.ItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.StockUnit)
            .WithMany()
            .HasForeignKey(s => s.StockUnitId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(s => s.ItemId);
        builder.HasIndex(s => s.ClientId);
        builder.HasIndex(s => s.InvoiceNo);
    }
}
