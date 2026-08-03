using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NestFlow_Backend.Models.Entities;

namespace NestFlow_Backend.Data.Configurations;

public class WorkspaceMembershipConfiguration : IEntityTypeConfiguration<WorkspaceMembership>
{
    public void Configure(EntityTypeBuilder<WorkspaceMembership> builder)
    {
        builder.ToTable("workspace_memberships");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.WorkspaceId).HasColumnName("workspace_id").IsRequired();
        builder.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(x => x.MembershipType).HasColumnName("membership_type").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.JoinedAt).HasColumnName("joined_at").IsRequired();

        // 計畫第 13 節指定的唯一鍵：workspace_id + user_id
        builder.HasIndex(x => new { x.WorkspaceId, x.UserId })
            .IsUnique()
            .HasDatabaseName("ux_workspace_memberships_workspace_user");

        builder.HasOne(x => x.Workspace)
            .WithMany(x => x.Memberships)
            .HasForeignKey(x => x.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.User)
            .WithMany(x => x.Memberships)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
