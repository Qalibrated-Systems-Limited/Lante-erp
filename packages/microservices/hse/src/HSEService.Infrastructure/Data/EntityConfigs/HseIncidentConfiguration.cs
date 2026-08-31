using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HSEService.Core.Entities;

namespace HSEService.Infrastructure.Data.EntityConfigs;

public class HseIncidentConfiguration : IEntityTypeConfiguration<HseIncident>
{
    public void Configure(EntityTypeBuilder<HseIncident> builder)
    {
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Type).HasConversion<int>();
        builder.Property(i => i.Severity).HasConversion<int>();
        builder.Property(i => i.Status).HasConversion<int>();
        builder.HasIndex(i => i.SiteId);
        builder.HasIndex(i => i.OccurredAt);
    }
}
