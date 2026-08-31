using StoreService.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace StoreService.Infrastructure.Data.Configurations;

public class StoreTransferConfiguration : IEntityTypeConfiguration<StoreTransfer>
{
    public void Configure(EntityTypeBuilder<StoreTransfer> builder)
    {
        builder.ToTable("store_transfers");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Qty).HasColumnType("numeric(18,2)");
        builder.Property(t => t.ApprovedBy).HasMaxLength(255);
        builder.Property(t => t.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasOne(t => t.Item)
            .WithMany()
            .HasForeignKey(t => t.ItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.FromLocation)
            .WithMany()
            .HasForeignKey(t => t.FromLocationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.ToLocation)
            .WithMany()
            .HasForeignKey(t => t.ToLocationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(t => t.ItemId);
        builder.HasIndex(t => t.Status);
    }
}
