using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ComplianceService.Core.Entities;

namespace ComplianceService.Infrastructure.Data.EntityConfigs;

public class AnnualReturnConfiguration : IEntityTypeConfiguration<AnnualReturn>
{
    public void Configure(EntityTypeBuilder<AnnualReturn> builder)
    {
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Status).HasConversion<int>();
        builder.HasIndex(r => r.Year).IsUnique();
    }
}
