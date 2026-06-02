using PaymentLedger.Domain.Enums;

namespace PaymentLedger.Domain.Entities;

public class Transaction
{

    public Guid Id { get; private set; }

    public string TransactionCode { get; private set; } = string.Empty;

    public TransactionType Type { get; private set; }

    public TransactionStatus Status { get; private set; }

    public Guid? SourceWalletId { get; private set; }

    public Guid? DestinationWalletId { get; private set; }

    public decimal Amount { get; private set; }

    public string Currency { get; private set; } = string.Empty;

    public string IdempotencyKey { get; private set; } = string.Empty;

    public Guid? ReferenceTransactionId { get; private set; }

    public string? Description { get; private set; }

    public string? Metadata { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

}
