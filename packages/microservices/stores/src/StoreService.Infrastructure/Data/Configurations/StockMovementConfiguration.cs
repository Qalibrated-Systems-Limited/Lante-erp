using StoreService.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace StoreService.Infrastructure.Data.Configurations;

public class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> builder)
    {
        builder.ToTable("stock_movements");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Quantity).HasColumnType("numeric(18,2)");
        builder.Property(m => m.BalanceAfter).HasColumnType("numeric(18,2)");
        builder.Property(m => m.Reference).IsRequired().HasMaxLength(100);
        builder.Property(m => m.Type).HasConversion<string>().HasMaxLength(20);

        builder.HasOne(m => m.Item)
            .WithMany()
            .HasForeignKey(m => m.ItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(m => m.Location)
            .WithMany()
            .HasForeignKey(m => m.LocationId)
            .OnDelete(DeleteBehavior.Restrict);

        // The hot-path query: per-location running balance for an item.
        builder.HasIndex(m => new { m.ItemId, m.LocationId });
        builder.HasIndex(m => m.Reference);
    }
}
