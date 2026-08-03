using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NestFlow_Backend.Models.Entities;

namespace NestFlow_Backend.Data.Configurations;

public class ReminderConfiguration : IEntityTypeConfiguration<Reminder>
{
    public void Configure(EntityTypeBuilder<Reminder> builder)
    {
        builder.ToTable("reminders");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(x => x.WorkspaceId).HasColumnName("workspace_id").IsRequired();
        builder.Property(x => x.Content).HasColumnName("content").HasMaxLength(200).IsRequired();
        builder.Property(x => x.TriggerAt).HasColumnName("trigger_at").IsRequired();
        builder.Property(x => x.NotificationProvider)
            .HasColumnName("notification_provider").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.RetryCount).HasColumnName("retry_count").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();

        // Worker 每次掃描都是「找出到期且待發送的提醒」
        builder.HasIndex(x => new { x.Status, x.TriggerAt })
            .HasDatabaseName("ix_reminders_status_trigger");

        // PWA 依 Workspace 列出提醒
        builder.HasIndex(x => new { x.WorkspaceId, x.TriggerAt })
            .HasDatabaseName("ix_reminders_workspace_trigger");

        builder.HasOne(x => x.Workspace)
            .WithMany()
            .HasForeignKey(x => x.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
