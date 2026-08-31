using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TicketingService.Core.Entities;

namespace TicketingService.Infrastructure.Data.EntityConfigs;

public class TicketHistoryConfiguration : IEntityTypeConfiguration<TicketHistory>
{
    public void Configure(EntityTypeBuilder<TicketHistory> builder)
    {
        builder.HasKey(h => h.Id);
        builder.Property(h => h.TicketId).IsRequired().HasMaxLength(100);
        builder.Property(h => h.UserId).IsRequired().HasMaxLength(100);
        builder.Property(h => h.Action).IsRequired().HasMaxLength(100);

        builder.HasIndex(h => h.TicketId);
        builder.HasIndex(h => h.OccurredAt);
    }
}
