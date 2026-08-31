using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SubcontractsService.Core.Entities;

namespace SubcontractsService.Infrastructure.Data.EntityConfigs;

public class SubcontractAwardConfiguration : IEntityTypeConfiguration<SubcontractAward>
{
    public void Configure(EntityTypeBuilder<SubcontractAward> builder)
    {
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Status).HasConversion<int>();
        builder.HasIndex(a => a.SubcontractorId);
        builder.HasIndex(a => a.ProjectId);
    }
}
