using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NestFlow_Backend.Models.Entities;

namespace NestFlow_Backend.Data.Configurations;

public class AccountEntryShareConfiguration : IEntityTypeConfiguration<AccountEntryShare>
{
    public void Configure(EntityTypeBuilder<AccountEntryShare> builder)
    {
        builder.ToTable("account_entry_shares");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.AccountEntryId).HasColumnName("account_entry_id").IsRequired();
        builder.Property(x => x.UserId).HasColumnName("user_id");
        builder.Property(x => x.ParticipantName).HasColumnName("participant_name").HasMaxLength(80).IsRequired();
        builder.Property(x => x.Amount).HasColumnName("amount").HasPrecision(18, 2).IsRequired();
        builder.HasIndex(x => x.AccountEntryId).HasDatabaseName("ix_account_entry_shares_entry");
        builder.HasOne(x => x.AccountEntry).WithMany(x => x.Shares).HasForeignKey(x => x.AccountEntryId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction);
    }
}
