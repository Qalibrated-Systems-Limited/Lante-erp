using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ComplianceService.Core.Entities;

namespace ComplianceService.Infrastructure.Data.EntityConfigs;

public class RelatedPartyConfiguration : IEntityTypeConfiguration<RelatedParty>
{
    public void Configure(EntityTypeBuilder<RelatedParty> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Relationship).HasConversion<int>();
        builder.HasIndex(p => p.RegNo);
    }
}
