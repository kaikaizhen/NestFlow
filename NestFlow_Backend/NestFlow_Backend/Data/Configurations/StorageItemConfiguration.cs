using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NestFlow_Backend.Models.Entities;

namespace NestFlow_Backend.Data.Configurations;

public class StorageItemConfiguration : IEntityTypeConfiguration<StorageItem>
{
    public void Configure(EntityTypeBuilder<StorageItem> builder)
    {
        builder.ToTable("storage_items");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(x => x.WorkspaceId).HasColumnName("workspace_id").IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        builder.Property(x => x.Location).HasColumnName("location").HasMaxLength(100).IsRequired();
        builder.Property(x => x.Note).HasColumnName("note").HasMaxLength(200);
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();

        // 最近更新列表是預設畫面，搜尋則另外掃描名稱與位置
        builder.HasIndex(x => new { x.WorkspaceId, x.Status, x.UpdatedAt })
            .HasDatabaseName("ix_storage_items_workspace_status_updated");

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
