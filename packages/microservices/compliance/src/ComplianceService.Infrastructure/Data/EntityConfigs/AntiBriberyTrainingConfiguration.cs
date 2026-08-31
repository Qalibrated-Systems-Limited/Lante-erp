using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ComplianceService.Core.Entities;

namespace ComplianceService.Infrastructure.Data.EntityConfigs;

public class AntiBriberyTrainingConfiguration : IEntityTypeConfiguration<AntiBriberyTraining>
{
    public void Configure(EntityTypeBuilder<AntiBriberyTraining> builder)
    {
        builder.HasKey(t => t.Id);
        builder.HasIndex(t => t.NextDueOn);
    }
}
