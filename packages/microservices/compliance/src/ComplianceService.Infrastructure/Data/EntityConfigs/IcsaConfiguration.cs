using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ComplianceService.Core.Entities;

namespace ComplianceService.Infrastructure.Data.EntityConfigs;

public class IcsaConfiguration : IEntityTypeConfiguration<Icsa>
{
    public void Configure(EntityTypeBuilder<Icsa> builder)
    {
        builder.HasKey(a => a.Id);
        builder.HasIndex(a => a.RelatedPartyId);
    }
}
