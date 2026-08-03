using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NestFlow_Backend.Models.Entities;

namespace NestFlow_Backend.Data.Configurations;

public class AccountEntryConfiguration : IEntityTypeConfiguration<AccountEntry>
{
    public void Configure(EntityTypeBuilder<AccountEntry> builder)
    {
        builder.ToTable("account_entries");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(x => x.WorkspaceId).HasColumnName("workspace_id").IsRequired();
        builder.Property(x => x.Type).HasColumnName("type").HasConversion<string>().HasMaxLength(20).IsRequired();
        // 金額使用 decimal 避免浮點誤差
        builder.Property(x => x.Amount).HasColumnName("amount").HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.Currency).HasColumnName("currency").HasMaxLength(3).IsRequired();
        builder.Property(x => x.Category).HasColumnName("category").HasMaxLength(30).IsRequired();
        builder.Property(x => x.Note).HasColumnName("note").HasMaxLength(200);
        builder.Property(x => x.OccurredAt).HasColumnName("occurred_at").IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();

        // 依 Workspace 取區間資料是最主要的查詢方式
        builder.HasIndex(x => new { x.WorkspaceId, x.Status, x.OccurredAt })
            .HasDatabaseName("ix_account_entries_workspace_status_occurred");

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
