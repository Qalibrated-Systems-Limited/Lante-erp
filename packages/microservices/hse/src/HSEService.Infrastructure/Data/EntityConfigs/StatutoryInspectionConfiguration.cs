using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HSEService.Core.Entities;

namespace HSEService.Infrastructure.Data.EntityConfigs;

public class StatutoryInspectionConfiguration : IEntityTypeConfiguration<StatutoryInspection>
{
    public void Configure(EntityTypeBuilder<StatutoryInspection> builder)
    {
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Status).HasConversion<int>();
        builder.HasIndex(i => i.DueDate);
    }
}
