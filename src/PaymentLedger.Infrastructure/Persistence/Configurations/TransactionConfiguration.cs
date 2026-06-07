using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PaymentLedger.Domain.Entities;

namespace PaymentLedger.Infrastructure.Persistence.Configurations;

public sealed class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.ToTable("transactions", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint("ck_transactions_amount_positive", "\"amount\" > 0");
        });

        builder.HasKey(transaction => transaction.Id);

        builder.Property(transaction => transaction.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(transaction => transaction.TransactionCode)
            .HasColumnName("transaction_code")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(transaction => transaction.Type)
            .HasColumnName("type")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(transaction => transaction.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(transaction => transaction.SourceWalletId)
            .HasColumnName("source_wallet_id");

        builder.Property(transaction => transaction.DestinationWalletId)
            .HasColumnName("destination_wallet_id");

        builder.Property(transaction => transaction.Amount)
            .HasColumnName("amount")
            .HasColumnType("numeric(18,2)")
            .IsRequired();

        builder.Property(transaction => transaction.Currency)
            .HasColumnName("currency")
            .HasMaxLength(3)
            .IsRequired();

        builder.Property(transaction => transaction.IdempotencyKey)
            .HasColumnName("idempotency_key")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(transaction => transaction.ReferenceTransactionId)
            .HasColumnName("reference_transaction_id");

        builder.Property(transaction => transaction.Description)
            .HasColumnName("description")
            .HasMaxLength(500);

        builder.Property(transaction => transaction.Metadata)
            .HasColumnName("metadata")
            .HasColumnType("jsonb");

        builder.Property(transaction => transaction.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(transaction => transaction.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.HasIndex(transaction => transaction.TransactionCode);
        builder.HasIndex(transaction => transaction.IdempotencyKey);
        builder.HasIndex(transaction => new { transaction.SourceWalletId, transaction.CreatedAt });
        builder.HasIndex(transaction => new { transaction.DestinationWalletId, transaction.CreatedAt });
        builder.HasIndex(transaction => transaction.ReferenceTransactionId);
        builder.HasIndex(transaction => transaction.Status);
        builder.HasIndex(transaction => transaction.Type);
        builder.HasIndex(transaction => transaction.CreatedAt);

        builder.HasOne<Wallet>()
            .WithMany()
            .HasForeignKey(transaction => transaction.SourceWalletId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Wallet>()
            .WithMany()
            .HasForeignKey(transaction => transaction.DestinationWalletId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Transaction>()
            .WithMany()
            .HasForeignKey(transaction => transaction.ReferenceTransactionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
