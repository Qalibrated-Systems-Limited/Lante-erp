using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ComplianceService.Core.Entities;

namespace ComplianceService.Infrastructure.Data.EntityConfigs;

public class CaseActionConfiguration : IEntityTypeConfiguration<CaseAction>
{
    public void Configure(EntityTypeBuilder<CaseAction> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.ParentType).HasConversion<int>();
        builder.HasIndex(c => new { c.ParentType, c.ParentId });
    }
}
