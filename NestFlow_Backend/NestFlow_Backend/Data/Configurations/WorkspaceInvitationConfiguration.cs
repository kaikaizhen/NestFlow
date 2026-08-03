using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NestFlow_Backend.Models.Entities;

namespace NestFlow_Backend.Data.Configurations;

public class WorkspaceInvitationConfiguration : IEntityTypeConfiguration<WorkspaceInvitation>
{
    public void Configure(EntityTypeBuilder<WorkspaceInvitation> builder)
    {
        builder.ToTable("workspace_invitations");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.WorkspaceId).HasColumnName("workspace_id").IsRequired();
        builder.Property(x => x.CodeHash).HasColumnName("code_hash").HasMaxLength(64).IsRequired();
        builder.Property(x => x.ExpiresAt).HasColumnName("expires_at").IsRequired();
        builder.Property(x => x.UsedAt).HasColumnName("used_at");
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasIndex(x => x.CodeHash)
            .IsUnique()
            .HasDatabaseName("ux_workspace_invitations_code_hash");

        builder.HasOne(x => x.Workspace)
            .WithMany()
            .HasForeignKey(x => x.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
