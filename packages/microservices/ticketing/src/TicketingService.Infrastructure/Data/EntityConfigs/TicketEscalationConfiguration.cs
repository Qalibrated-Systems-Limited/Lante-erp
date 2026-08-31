using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TicketingService.Core.Entities;

namespace TicketingService.Infrastructure.Data.EntityConfigs;

public class TicketEscalationConfiguration : IEntityTypeConfiguration<TicketEscalation>
{
    public void Configure(EntityTypeBuilder<TicketEscalation> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.EscalationLevel).HasConversion<int>();
        builder.Property(e => e.EscalatedToUserId).IsRequired().HasMaxLength(100);
        builder.Property(e => e.Reason).IsRequired();

        builder.HasIndex(e => e.TicketId);
        builder.HasIndex(e => e.IsAcknowledged);
    }
}
