using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TicketingService.Core.Entities;

namespace TicketingService.Infrastructure.Data.EntityConfigs;

public class EscalationRuleConfiguration : IEntityTypeConfiguration<EscalationRule>
{
    public void Configure(EntityTypeBuilder<EscalationRule> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Priority).HasConversion<int>();
        builder.Property(e => e.EscalationLevel).HasConversion<int>();
        builder.HasIndex(e => new { e.CategoryId, e.Priority });
    }
}
