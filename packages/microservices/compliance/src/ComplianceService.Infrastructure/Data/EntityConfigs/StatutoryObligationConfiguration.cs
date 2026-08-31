using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ComplianceService.Core.Entities;

namespace ComplianceService.Infrastructure.Data.EntityConfigs;

public class StatutoryObligationConfiguration : IEntityTypeConfiguration<StatutoryObligation>
{
    public void Configure(EntityTypeBuilder<StatutoryObligation> builder)
    {
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Frequency).HasConversion<int>();
    }
}
