using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ComplianceService.Core.Entities;

namespace ComplianceService.Infrastructure.Data.EntityConfigs;

public class CosecTaskConfiguration : IEntityTypeConfiguration<CosecTask>
{
    public void Configure(EntityTypeBuilder<CosecTask> builder)
    {
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Status).HasConversion<int>();
        builder.HasIndex(t => t.DueDate);
    }
}
