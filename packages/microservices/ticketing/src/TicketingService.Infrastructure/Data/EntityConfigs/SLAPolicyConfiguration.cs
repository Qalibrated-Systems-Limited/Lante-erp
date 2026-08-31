using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TicketingService.Core.Entities;

namespace TicketingService.Infrastructure.Data.EntityConfigs;

public class SLAPolicyConfiguration : IEntityTypeConfiguration<SLAPolicy>
{
    public void Configure(EntityTypeBuilder<SLAPolicy> builder)
    {
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Priority).HasConversion<int>();
        builder.HasIndex(s => new { s.CategoryId, s.Priority }).IsUnique();
    }
}
