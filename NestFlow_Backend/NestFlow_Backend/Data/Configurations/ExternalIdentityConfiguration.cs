using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NestFlow_Backend.Models.Entities;

namespace NestFlow_Backend.Data.Configurations;

public class ExternalIdentityConfiguration : IEntityTypeConfiguration<ExternalIdentity>
{
    public void Configure(EntityTypeBuilder<ExternalIdentity> builder)
    {
        builder.ToTable("external_identities");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(x => x.Provider).HasColumnName("provider").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.ChannelId).HasColumnName("channel_id").HasMaxLength(64).IsRequired();
        builder.Property(x => x.ExternalSubject).HasColumnName("external_subject").HasMaxLength(500).IsRequired();
        builder.Property(x => x.ExternalSubjectHash).HasColumnName("external_subject_hash").HasMaxLength(64).IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();

        // 同一個 Provider 與 Channel 下，一個外部帳號只能對應一位本地使用者
        builder.HasIndex(x => new { x.Provider, x.ChannelId, x.ExternalSubjectHash })
            .IsUnique()
            .HasDatabaseName("ux_external_identities_provider_channel_subject");

        builder.HasOne(x => x.User)
            .WithMany(x => x.ExternalIdentities)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
