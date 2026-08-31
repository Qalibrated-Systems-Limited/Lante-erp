using StoreService.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace StoreService.Infrastructure.Data.Configurations;

public class PurchasePriceHistoryConfiguration : IEntityTypeConfiguration<PurchasePriceHistory>
{
    public void Configure(EntityTypeBuilder<PurchasePriceHistory> builder)
    {
        builder.ToTable("purchase_price_histories");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Price).HasColumnType("numeric(18,4)");
        builder.Property(p => p.VariancePct).HasColumnType("numeric(9,4)");
        builder.Property(p => p.AlertLevel).HasConversion<string>().HasMaxLength(20);

        builder.HasOne(p => p.Item)
            .WithMany()
            .HasForeignKey(p => p.ItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.Supplier)
            .WithMany()
            .HasForeignKey(p => p.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => new { p.ItemId, p.SupplierId, p.PurchasedOn });
    }
}
