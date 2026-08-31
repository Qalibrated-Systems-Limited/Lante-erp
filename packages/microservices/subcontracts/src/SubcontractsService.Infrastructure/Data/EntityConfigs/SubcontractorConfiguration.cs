using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SubcontractsService.Core.Entities;

namespace SubcontractsService.Infrastructure.Data.EntityConfigs;

public class SubcontractorConfiguration : IEntityTypeConfiguration<Subcontractor>
{
    public void Configure(EntityTypeBuilder<Subcontractor> builder)
    {
        builder.HasKey(s => s.Id);
        builder.HasIndex(s => s.TradeCategory);
    }
}
