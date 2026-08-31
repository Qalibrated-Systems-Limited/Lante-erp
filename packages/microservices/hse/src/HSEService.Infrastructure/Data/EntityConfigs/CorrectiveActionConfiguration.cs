using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HSEService.Core.Entities;

namespace HSEService.Infrastructure.Data.EntityConfigs;

public class CorrectiveActionConfiguration : IEntityTypeConfiguration<CorrectiveAction>
{
    public void Configure(EntityTypeBuilder<CorrectiveAction> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Status).HasConversion<int>();
        builder.HasIndex(c => c.IncidentId);
    }
}
