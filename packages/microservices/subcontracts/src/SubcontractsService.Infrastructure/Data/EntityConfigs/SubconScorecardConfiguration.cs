using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SubcontractsService.Core.Entities;

namespace SubcontractsService.Infrastructure.Data.EntityConfigs;

public class SubconScorecardConfiguration : IEntityTypeConfiguration<SubconScorecard>
{
    public void Configure(EntityTypeBuilder<SubconScorecard> builder)
    {
        builder.HasKey(s => s.Id);
        builder.HasIndex(s => s.AwardId);
    }
}
