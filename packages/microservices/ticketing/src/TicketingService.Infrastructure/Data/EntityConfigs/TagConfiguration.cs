using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TicketingService.Core.Entities;

namespace TicketingService.Infrastructure.Data.EntityConfigs;

public class TagConfiguration : IEntityTypeConfiguration<Tag>
{
    public void Configure(EntityTypeBuilder<Tag> builder)
    {
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Name).IsRequired().HasMaxLength(100);
        builder.Property(t => t.Color).HasMaxLength(20);
        builder.Property(t => t.Description).HasMaxLength(500);
        builder.HasIndex(t => t.Name).IsUnique();
    }
}

public class TicketTagConfiguration : IEntityTypeConfiguration<TicketTag>
{
    public void Configure(EntityTypeBuilder<TicketTag> builder)
    {
        builder.HasKey(tt => tt.Id);

        builder.HasOne(tt => tt.Ticket)
            .WithMany(t => t.Tags)
            .HasForeignKey(tt => tt.TicketId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(tt => tt.Tag)
            .WithMany(t => t.TicketTags)
            .HasForeignKey(tt => tt.TagId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(tt => new { tt.TicketId, tt.TagId }).IsUnique();
    }
}
