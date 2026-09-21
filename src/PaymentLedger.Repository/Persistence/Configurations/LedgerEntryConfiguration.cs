using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PaymentLedger.Repository.Entities;

namespace PaymentLedger.Repository.Persistence.Configurations;

public sealed class LedgerEntryConfiguration : IEntityTypeConfiguration<LedgerEntry>
{
    public void Configure(EntityTypeBuilder<LedgerEntry> builder)
    {
        builder.ToTable("ledger_entries", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint("ck_ledger_entries_amount_positive", "\"amount\" > 0");
        });

        builder.HasKey(entry => entry.Id);

        builder.Property(entry => entry.Id)
            .HasColumnName("ledger_entry_id")
            .UseIdentityByDefaultColumn();

        builder.Property(entry => entry.TransactionId)
            .HasColumnName("transaction_id")
            .IsRequired();

        builder.Property(entry => entry.WalletId)
            .HasColumnName("wallet_id")
            .IsRequired();

        builder.Property(entry => entry.EntryType)
            .HasColumnName("entry_type")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(entry => entry.Amount)
            .HasColumnName("amount")
            .HasColumnType("numeric(18,2)")
            .IsRequired();

        builder.Property(entry => entry.Currency)
            .HasColumnName("currency")
            .HasMaxLength(3)
            .IsRequired();

        builder.Property(entry => entry.BalanceAfterTransaction)
            .HasColumnName("balance_after_transaction")
            .HasColumnType("numeric(18,2)");

        builder.Property(entry => entry.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.HasIndex(entry => entry.TransactionId);
        builder.HasIndex(entry => new { entry.WalletId, entry.CreatedAt });
        builder.HasIndex(entry => entry.EntryType);

        builder.HasOne<Transaction>()
            .WithMany()
            .HasForeignKey(entry => entry.TransactionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Wallet>()
            .WithMany()
            .HasForeignKey(entry => entry.WalletId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
