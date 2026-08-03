using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NestFlow_Backend.Models.Entities;

namespace NestFlow_Backend.Data.Configurations;

public class PendingActionConfiguration : IEntityTypeConfiguration<PendingAction>
{
    public void Configure(EntityTypeBuilder<PendingAction> builder)
    {
        builder.ToTable("pending_actions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(x => x.WorkspaceId).HasColumnName("workspace_id").IsRequired();
        builder.Property(x => x.Provider).HasColumnName("provider").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.ActionType).HasColumnName("action_type").HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(x => x.PayloadJson).HasColumnName("payload_json").HasMaxLength(2000).IsRequired();
        builder.Property(x => x.SchemaVersion).HasColumnName("schema_version").HasMaxLength(20).IsRequired();
        builder.Property(x => x.WorkflowVersion).HasColumnName("workflow_version").HasMaxLength(60).IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.ExpiresAt).HasColumnName("expires_at").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();

        // 主要查詢：找出某位使用者在某個來源上尚待確認的動作
        builder.HasIndex(x => new { x.UserId, x.Provider, x.Status })
            .HasDatabaseName("ix_pending_actions_user_provider_status");
    }
}
