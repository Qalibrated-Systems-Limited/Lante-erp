using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ComplianceService.Core.Entities;

namespace ComplianceService.Infrastructure.Data.EntityConfigs;

public class WhistleblowerCaseConfiguration : IEntityTypeConfiguration<WhistleblowerCase>
{
    public void Configure(EntityTypeBuilder<WhistleblowerCase> builder)
    {
        builder.HasKey(w => w.Id);
        builder.Property(w => w.Status).HasConversion<int>();
        builder.HasIndex(w => w.RefNo).IsUnique();
    }
}
