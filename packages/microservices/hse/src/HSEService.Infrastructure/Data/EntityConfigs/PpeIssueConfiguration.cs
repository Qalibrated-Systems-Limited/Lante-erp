using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HSEService.Core.Entities;

namespace HSEService.Infrastructure.Data.EntityConfigs;

public class PpeIssueConfiguration : IEntityTypeConfiguration<PpeIssue>
{
    public void Configure(EntityTypeBuilder<PpeIssue> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Condition).HasConversion<int>();
    }
}
