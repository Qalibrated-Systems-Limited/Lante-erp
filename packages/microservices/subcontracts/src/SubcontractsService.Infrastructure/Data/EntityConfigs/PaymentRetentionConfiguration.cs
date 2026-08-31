using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SubcontractsService.Core.Entities;

namespace SubcontractsService.Infrastructure.Data.EntityConfigs;

public class PaymentRetentionConfiguration : IEntityTypeConfiguration<PaymentRetention>
{
    public void Configure(EntityTypeBuilder<PaymentRetention> builder)
    {
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Status).HasConversion<int>();
        builder.HasIndex(p => p.AwardId);
    }
}
