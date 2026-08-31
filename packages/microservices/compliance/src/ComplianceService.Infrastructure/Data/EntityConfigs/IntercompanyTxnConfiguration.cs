using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ComplianceService.Core.Entities;

namespace ComplianceService.Infrastructure.Data.EntityConfigs;

public class IntercompanyTxnConfiguration : IEntityTypeConfiguration<IntercompanyTxn>
{
    public void Configure(EntityTypeBuilder<IntercompanyTxn> builder)
    {
        builder.HasKey(t => t.Id);
        builder.HasIndex(t => t.IcsaId);
        builder.HasIndex(t => t.RelatedPartyId);
        builder.HasIndex(t => t.ReconciledAt);
    }
}
