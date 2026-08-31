using StoreService.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace StoreService.Infrastructure.Data.Configurations;

public class StoreAuditLogConfiguration : IEntityTypeConfiguration<StoreAuditLog>
{
    public void Configure(EntityTypeBuilder<StoreAuditLog> builder)
    {
        builder.ToTable("store_audit_logs");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Entity).IsRequired().HasMaxLength(200);
        builder.Property(x => x.EntityId).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Action).IsRequired().HasMaxLength(20);

        builder.HasIndex(x => x.At);
        builder.HasIndex(x => new { x.Entity, x.EntityId });
    }
}
