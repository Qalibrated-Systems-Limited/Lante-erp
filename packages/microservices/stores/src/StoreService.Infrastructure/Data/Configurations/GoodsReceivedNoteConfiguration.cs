using StoreService.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace StoreService.Infrastructure.Data.Configurations;

public class GoodsReceivedNoteConfiguration : IEntityTypeConfiguration<GoodsReceivedNote>
{
    public void Configure(EntityTypeBuilder<GoodsReceivedNote> builder)
    {
        builder.ToTable("goods_received_notes");
        builder.HasKey(g => g.Id);

        builder.Property(g => g.QtyReceived).HasColumnType("numeric(18,2)");
        builder.Property(g => g.LandedCost).HasColumnType("numeric(18,2)");
        builder.Property(g => g.AcceptedQty).HasColumnType("numeric(18,2)");
        builder.Property(g => g.RejectedQty).HasColumnType("numeric(18,2)");
        builder.Property(g => g.InspectedBy).HasMaxLength(255);
        builder.Property(g => g.Notes).HasMaxLength(1000);
        builder.Property(g => g.InspectionStatus).HasConversion<string>().HasMaxLength(20);

        builder.HasOne(g => g.Item)
            .WithMany()
            .HasForeignKey(g => g.ItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(g => g.Supplier)
            .WithMany()
            .HasForeignKey(g => g.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(g => g.Location)
            .WithMany()
            .HasForeignKey(g => g.LocationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(g => g.ItemId);
        builder.HasIndex(g => g.SupplierId);
        builder.HasIndex(g => g.LocationId);
        builder.HasIndex(g => g.InspectionStatus);
    }
}
