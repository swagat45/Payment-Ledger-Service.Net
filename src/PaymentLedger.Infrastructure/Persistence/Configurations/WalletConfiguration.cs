using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PaymentLedger.Domain.Entities;

namespace PaymentLedger.Infrastructure.Persistence.Configurations;

public sealed class WalletConfiguration : IEntityTypeConfiguration<Wallet>
{
    public void Configure(EntityTypeBuilder<Wallet> builder)
    {
        builder.ToTable("wallets", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint("ck_wallets_balance_non_negative", "\"balance\" >= 0");
        });

        builder.HasKey(wallet => wallet.Id);

        builder.Property(wallet => wallet.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(wallet => wallet.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(wallet => wallet.Currency)
            .HasColumnName("currency")
            .HasMaxLength(3)
            .IsRequired();

        builder.Property(wallet => wallet.Balance)
            .HasColumnName("balance")
            .HasColumnType("numeric(18,2)")
            .IsRequired();

        builder.Property(wallet => wallet.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(wallet => wallet.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(wallet => wallet.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.HasIndex(wallet => new { wallet.UserId, wallet.Currency })
            .IsUnique();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(wallet => wallet.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
