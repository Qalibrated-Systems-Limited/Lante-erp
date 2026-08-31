using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TicketingService.Core.Entities;

namespace TicketingService.Infrastructure.Data.EntityConfigs;

public class TicketCommentConfiguration : IEntityTypeConfiguration<TicketComment>
{
    public void Configure(EntityTypeBuilder<TicketComment> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Content).IsRequired().HasMaxLength(2000);
        builder.Property(c => c.AuthorUserId).IsRequired().HasMaxLength(100);

        builder.HasMany(c => c.Attachments)
            .WithOne(a => a.TicketComment)
            .HasForeignKey(a => a.TicketCommentId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(c => c.TicketId);
    }
}
