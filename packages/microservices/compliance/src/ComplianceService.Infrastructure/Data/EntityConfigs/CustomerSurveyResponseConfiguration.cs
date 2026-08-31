using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ComplianceService.Core.Entities;

namespace ComplianceService.Infrastructure.Data.EntityConfigs;

public class CustomerSurveyResponseConfiguration : IEntityTypeConfiguration<CustomerSurveyResponse>
{
    public void Configure(EntityTypeBuilder<CustomerSurveyResponse> builder)
    {
        builder.HasKey(r => r.Id);
        builder.Property(r => r.FeedbackType).HasConversion<int>();
        builder.Property(r => r.Rating).HasConversion<int>();
        builder.HasIndex(r => r.SubmittedAt);
    }
}
