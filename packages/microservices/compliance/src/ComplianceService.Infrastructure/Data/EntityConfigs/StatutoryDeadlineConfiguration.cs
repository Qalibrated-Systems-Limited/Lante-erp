using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ComplianceService.Core.Entities;

namespace ComplianceService.Infrastructure.Data.EntityConfigs;

public class StatutoryDeadlineConfiguration : IEntityTypeConfiguration<StatutoryDeadline>
{
    public void Configure(EntityTypeBuilder<StatutoryDeadline> builder)
    {
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Status).HasConversion<int>();
        builder.HasIndex(d => d.DueDate);
        builder.HasIndex(d => new { d.ObligationId, d.DueDate }).IsUnique();
    }
}
