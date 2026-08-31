using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HSEService.Core.Entities;

namespace HSEService.Infrastructure.Data.EntityConfigs;

public class HseTrainingRecordConfiguration : IEntityTypeConfiguration<HseTrainingRecord>
{
    public void Configure(EntityTypeBuilder<HseTrainingRecord> builder)
    {
        builder.HasKey(t => t.Id);
        builder.HasIndex(t => t.ExpiresOn);
    }
}
