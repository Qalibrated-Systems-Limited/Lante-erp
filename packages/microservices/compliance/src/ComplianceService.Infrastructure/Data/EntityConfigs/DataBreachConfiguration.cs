using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ComplianceService.Core.Entities;

namespace ComplianceService.Infrastructure.Data.EntityConfigs;

public class DataBreachConfiguration : IEntityTypeConfiguration<DataBreach>
{
    public void Configure(EntityTypeBuilder<DataBreach> builder)
    {
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Status).HasConversion<int>();
        builder.HasIndex(b => b.OdpcNotificationDueAt);
    }
}
