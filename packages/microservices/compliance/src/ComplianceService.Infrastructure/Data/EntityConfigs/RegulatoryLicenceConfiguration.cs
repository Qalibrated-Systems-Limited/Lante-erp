using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ComplianceService.Core.Entities;

namespace ComplianceService.Infrastructure.Data.EntityConfigs;

public class RegulatoryLicenceConfiguration : IEntityTypeConfiguration<RegulatoryLicence>
{
    public void Configure(EntityTypeBuilder<RegulatoryLicence> builder)
    {
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Type).HasConversion<int>();
        builder.HasIndex(l => l.ExpiryDate);
        builder.HasIndex(l => l.Type);
    }
}
