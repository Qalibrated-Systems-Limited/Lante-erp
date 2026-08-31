using LicenseService.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LicenseService.Infrastructure.Data.Configurations;

public class LicenseConfiguration : IEntityTypeConfiguration<License>
{
    public void Configure(EntityTypeBuilder<License> builder)
    {
        // Schema comes from the context's default (HasDefaultSchema): "licensing" for the
        // public-plane context, or the tenant schema (via search_path) for the tenant context.
        // Do NOT hardcode the schema here or it overrides both.
        builder.ToTable("licenses");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.Token).IsRequired().HasColumnType("TEXT");
        builder.Property(l => l.CustomerId).IsRequired().HasMaxLength(100);
        builder.Property(l => l.CustomerName).HasMaxLength(255);
        builder.Property(l => l.AppId).IsRequired().HasMaxLength(100);
        builder.Property(l => l.Features).IsRequired().HasColumnType("TEXT");
        builder.Property(l => l.MachineId).HasMaxLength(255);
        builder.Property(l => l.RevokeReason).HasMaxLength(500);
        builder.Property(l => l.LastMachineId).HasMaxLength(255);
        builder.Property(l => l.Notes).HasMaxLength(1000);

        // Unique token index — each JWT is unique
        builder.HasIndex(l => l.Token).IsUnique();

        // Query indexes
        builder.HasIndex(l => l.CustomerId);
        builder.HasIndex(l => l.AppId);
        builder.HasIndex(l => l.ExpiresAt);
        builder.HasIndex(l => new { l.CustomerId, l.AppId });
    }
}
