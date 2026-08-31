using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HSEService.Core.Entities;

namespace HSEService.Infrastructure.Data.EntityConfigs;

public class RamsConfiguration : IEntityTypeConfiguration<Rams>
{
    public void Configure(EntityTypeBuilder<Rams> builder)
    {
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Status).HasConversion<int>();
        builder.HasIndex(r => new { r.SiteId, r.Title, r.Version }).IsUnique();
    }
}
