using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TicketingService.Core.Entities;
using TicketingService.Core.Enums;

namespace TicketingService.Infrastructure.Data.EntityConfigs;

public class TicketConfiguration : IEntityTypeConfiguration<Ticket>
{
    public void Configure(EntityTypeBuilder<Ticket> builder)
    {
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Title).IsRequired().HasMaxLength(500);
        builder.Property(t => t.Description).IsRequired();
        builder.Property(t => t.DepartmentId).IsRequired().HasMaxLength(100);
        builder.Property(t => t.CreatedByUserId).IsRequired().HasMaxLength(100);

        builder.Property(t => t.Priority).HasConversion<int>();
        builder.Property(t => t.Status).HasConversion<int>();
        builder.Property(t => t.Source).HasConversion<int>();
        builder.Property(t => t.LinkedEntityType).HasConversion<int>();
        builder.Property(t => t.EscalationLevel).HasConversion<int?>();

        builder.HasOne(t => t.Category)
            .WithMany(c => c.Tickets)
            .HasForeignKey(t => t.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(t => t.Comments)
            .WithOne(c => c.Ticket)
            .HasForeignKey(c => c.TicketId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(t => t.Attachments)
            .WithOne(a => a.Ticket)
            .HasForeignKey(a => a.TicketId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(t => t.History)
            .WithOne(h => h.Ticket)
            .HasForeignKey(h => h.TicketId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(t => t.Assignments)
            .WithOne(a => a.Ticket)
            .HasForeignKey(a => a.TicketId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(t => t.Escalations)
            .WithOne(e => e.Ticket)
            .HasForeignKey(e => e.TicketId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(t => t.Watchers)
            .WithOne(w => w.Ticket)
            .HasForeignKey(w => w.TicketId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(t => t.Status);
        builder.HasIndex(t => t.Priority);
        builder.HasIndex(t => t.CategoryId);
        builder.HasIndex(t => t.DepartmentId);
        builder.HasIndex(t => t.AssignedToUserId);
        builder.HasIndex(t => t.CreatedByUserId);
        builder.HasIndex(t => t.CreatedAt);
    }
}
