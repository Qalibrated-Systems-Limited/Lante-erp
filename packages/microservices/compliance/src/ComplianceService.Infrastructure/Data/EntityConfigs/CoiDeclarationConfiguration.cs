using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ComplianceService.Core.Entities;

namespace ComplianceService.Infrastructure.Data.EntityConfigs;

public class CoiDeclarationConfiguration : IEntityTypeConfiguration<CoiDeclaration>
{
    public void Configure(EntityTypeBuilder<CoiDeclaration> builder)
    {
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Status).HasConversion<int>();
        builder.HasIndex(c => new { c.EmployeeUserId, c.Year }).IsUnique();
    }
}
