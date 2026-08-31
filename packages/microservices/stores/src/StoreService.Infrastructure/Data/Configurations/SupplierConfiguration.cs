using StoreService.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace StoreService.Infrastructure.Data.Configurations;

public class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> builder)
    {
        builder.ToTable("suppliers");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Name).IsRequired().HasMaxLength(255);
        builder.Property(s => s.KraPin).HasMaxLength(50);
        builder.Property(s => s.Rating).HasColumnType("numeric(3,2)");
        builder.Property(s => s.ContactPerson).HasMaxLength(255);
        builder.Property(s => s.Phone).HasMaxLength(50);
        builder.Property(s => s.Email).HasMaxLength(255);
        builder.Property(s => s.Address).HasMaxLength(500);

        builder.HasIndex(s => s.Name);
        builder.HasIndex(s => s.KraPin);
    }
}
