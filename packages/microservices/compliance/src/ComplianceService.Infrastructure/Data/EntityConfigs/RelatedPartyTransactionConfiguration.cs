using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ComplianceService.Core.Entities;

namespace ComplianceService.Infrastructure.Data.EntityConfigs;

public class RelatedPartyTransactionConfiguration : IEntityTypeConfiguration<RelatedPartyTransaction>
{
    public void Configure(EntityTypeBuilder<RelatedPartyTransaction> builder)
    {
        builder.HasKey(r => r.Id);
        builder.Property(r => r.RelationshipType).HasConversion<int>();
        builder.HasIndex(r => r.Reported);
    }
}
