using StoreService.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace StoreService.Infrastructure.Data.Configurations;

public class LocationConfiguration : IEntityTypeConfiguration<Location>
{
    public void Configure(EntityTypeBuilder<Location> builder)
    {
        builder.ToTable("locations");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.Code).IsRequired().HasMaxLength(50);
        builder.Property(l => l.Name).IsRequired().HasMaxLength(150);
        builder.Property(l => l.Type).HasConversion<string>().HasMaxLength(20);

        builder.HasIndex(l => l.Code).IsUnique();
    }
}
