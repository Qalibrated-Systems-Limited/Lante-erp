using StoreService.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace StoreService.Infrastructure.Data.Configurations;

public class StoreIssueNoteConfiguration : IEntityTypeConfiguration<StoreIssueNote>
{
    public void Configure(EntityTypeBuilder<StoreIssueNote> builder)
    {
        builder.ToTable("store_issue_notes");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.QtyIssued).HasColumnType("numeric(18,2)");
        builder.Property(s => s.CostCenter).IsRequired().HasMaxLength(100);
        builder.Property(s => s.IssuedTo).IsRequired().HasMaxLength(255);
        builder.Property(s => s.IssueType).HasConversion<string>().HasMaxLength(20);

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
        builder.HasIndex(s => s.CostCenter);
        builder.HasIndex(s => s.IssueType);
    }
}
