using StoreService.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace StoreService.Infrastructure.Data.Configurations;

public class StockUnitConfiguration : IEntityTypeConfiguration<StockUnit>
{
    public void Configure(EntityTypeBuilder<StockUnit> builder)
    {
        builder.ToTable("stock_units");
        builder.HasKey(u => u.Id);

        builder.Property(u => u.SerialNo).HasMaxLength(255);
        builder.Property(u => u.Qty).HasColumnType("numeric(18,2)");
        builder.Property(u => u.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasOne(u => u.Item)
            .WithMany()
            .HasForeignKey(u => u.ItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(u => u.Grn)
            .WithMany()
            .HasForeignKey(u => u.GrnId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(u => u.Location)
            .WithMany()
            .HasForeignKey(u => u.LocationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(u => u.SerialNo).IsUnique().HasFilter("\"SerialNo\" IS NOT NULL");
        builder.HasIndex(u => u.ItemId);
        builder.HasIndex(u => u.GrnId);
        builder.HasIndex(u => u.LocationId);
        builder.HasIndex(u => u.Status);
    }
}
