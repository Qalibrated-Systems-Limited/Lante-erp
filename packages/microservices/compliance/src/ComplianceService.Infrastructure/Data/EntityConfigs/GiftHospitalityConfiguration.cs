using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ComplianceService.Core.Entities;

namespace ComplianceService.Infrastructure.Data.EntityConfigs;

public class GiftHospitalityConfiguration : IEntityTypeConfiguration<GiftHospitality>
{
    public void Configure(EntityTypeBuilder<GiftHospitality> builder)
    {
        builder.HasKey(g => g.Id);
        builder.Property(g => g.Direction).HasConversion<int>();
        builder.HasIndex(g => g.Flagged);
    }
}
