using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HSEService.Core.Entities;

namespace HSEService.Infrastructure.Data.EntityConfigs;

public class ToolboxTalkConfiguration : IEntityTypeConfiguration<ToolboxTalk>
{
    public void Configure(EntityTypeBuilder<ToolboxTalk> builder)
    {
        builder.HasKey(t => t.Id);
        builder.HasIndex(t => t.SiteId);
    }
}
