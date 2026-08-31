using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TicketingService.Core.Entities;

namespace TicketingService.Infrastructure.Data.EntityConfigs;

public class TicketCategoryConfiguration : IEntityTypeConfiguration<TicketCategory>
{
    public void Configure(EntityTypeBuilder<TicketCategory> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Name).IsRequired().HasMaxLength(200);
        builder.Property(c => c.DepartmentId).IsRequired().HasMaxLength(100);
        builder.Property(c => c.DefaultPriority).HasConversion<int>();

        builder.HasMany(c => c.SLAPolicies)
            .WithOne(s => s.Category)
            .HasForeignKey(s => s.CategoryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(c => c.EscalationRules)
            .WithOne(e => e.Category)
            .HasForeignKey(e => e.CategoryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(c => c.Name).IsUnique();
        builder.HasIndex(c => c.DepartmentId);
    }
}
