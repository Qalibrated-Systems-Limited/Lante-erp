using StoreService.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace StoreService.Infrastructure.Data.Configurations;

public class StockTakeReconciliationConfiguration : IEntityTypeConfiguration<StockTakeReconciliation>
{
    public void Configure(EntityTypeBuilder<StockTakeReconciliation> builder)
    {
        builder.ToTable("stock_take_reconciliations");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.PhysicalCount).HasColumnType("numeric(18,2)");
        builder.Property(s => s.SystemCount).HasColumnType("numeric(18,2)");
        builder.Property(s => s.ApprovedBy).HasMaxLength(255);
        builder.Property(s => s.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(s => s.ReasonCode).HasConversion<string>().HasMaxLength(20);

        builder.Ignore(s => s.Variance);

        builder.HasOne(s => s.Item)
            .WithMany()
            .HasForeignKey(s => s.ItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.StockUnit)
            .WithMany()
            .HasForeignKey(s => s.StockUnitId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.Location)
            .WithMany()
            .HasForeignKey(s => s.LocationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(s => s.ItemId);
        builder.HasIndex(s => s.Status);
    }
}
