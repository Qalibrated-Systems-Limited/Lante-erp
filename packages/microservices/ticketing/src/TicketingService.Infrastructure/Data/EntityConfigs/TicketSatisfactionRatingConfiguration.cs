using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TicketingService.Core.Entities;

namespace TicketingService.Infrastructure.Data.EntityConfigs;

public class TicketSatisfactionRatingConfiguration : IEntityTypeConfiguration<TicketSatisfactionRating>
{
    public void Configure(EntityTypeBuilder<TicketSatisfactionRating> builder)
    {
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Rating).IsRequired();
        builder.Property(r => r.Comment).HasMaxLength(2000);
        builder.Property(r => r.SubmittedByUserId).IsRequired().HasMaxLength(100);

        builder.HasOne(r => r.Ticket)
            .WithOne(t => t.SatisfactionRating)
            .HasForeignKey<TicketSatisfactionRating>(r => r.TicketId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(r => r.TicketId).IsUnique();
    }
}
