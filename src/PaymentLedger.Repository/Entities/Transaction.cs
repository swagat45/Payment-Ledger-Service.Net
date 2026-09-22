using PaymentLedger.Repository.Enums;

namespace PaymentLedger.Repository.Entities;

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

    public Transaction()
    {
    }

    public static Transaction CreateCredit(
        Guid sourceWalletId,
        Guid destinationWalletId,
        decimal amount,
        string currency,
        string idempotencyKey,
        DateTimeOffset timestamp,
        string? description = null,
        string? metadata = null)
    {
        if (sourceWalletId == Guid.Empty)
        {
            throw new PaymentLedger.Repository.Exceptions.DomainException("Source wallet ID is required.");
        }

        if (destinationWalletId == Guid.Empty)
        {
            throw new PaymentLedger.Repository.Exceptions.DomainException("Destination wallet ID is required.");
        }

        if (amount <= 0)
        {
            throw new PaymentLedger.Repository.Exceptions.DomainException("Transaction amount must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(currency))
        {
            throw new PaymentLedger.Repository.Exceptions.DomainException("Currency is required.");
        }

        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            throw new PaymentLedger.Repository.Exceptions.DomainException("Idempotency key is required.");
        }

        var normalizedCurrency = currency.Trim().ToUpperInvariant();
        var codeSuffix = Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();
        var transactionCode = $"TXN-CR-{timestamp:yyyyMMddHHmmss}-{codeSuffix}";

        return new Transaction
        {
            Id = Guid.NewGuid(),
            TransactionCode = transactionCode,
            Type = TransactionType.Credit,
            Status = TransactionStatus.Posted,
            SourceWalletId = sourceWalletId,
            DestinationWalletId = destinationWalletId,
            Amount = decimal.Round(amount, 2, MidpointRounding.ToEven),
            Currency = normalizedCurrency,
            IdempotencyKey = idempotencyKey.Trim(),
            Description = description?.Trim(),
            Metadata = metadata,
            CreatedAt = timestamp,
            UpdatedAt = timestamp
        };
    }

    public static Transaction CreateDebit(
        Guid sourceWalletId,
        Guid destinationWalletId,
        decimal amount,
        string currency,
        string idempotencyKey,
        DateTimeOffset timestamp,
        string? description = null,
        string? metadata = null)
    {
        if (sourceWalletId == Guid.Empty)
        {
            throw new PaymentLedger.Repository.Exceptions.DomainException("Source wallet ID is required.");
        }

        if (destinationWalletId == Guid.Empty)
        {
            throw new PaymentLedger.Repository.Exceptions.DomainException("Destination wallet ID is required.");
        }

        if (amount <= 0)
        {
            throw new PaymentLedger.Repository.Exceptions.DomainException("Transaction amount must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(currency))
        {
            throw new PaymentLedger.Repository.Exceptions.DomainException("Currency is required.");
        }

        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            throw new PaymentLedger.Repository.Exceptions.DomainException("Idempotency key is required.");
        }

        var normalizedCurrency = currency.Trim().ToUpperInvariant();
        var codeSuffix = Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();
        var transactionCode = $"TXN-DB-{timestamp:yyyyMMddHHmmss}-{codeSuffix}";

        return new Transaction
        {
            Id = Guid.NewGuid(),
            TransactionCode = transactionCode,
            Type = TransactionType.Debit,
            Status = TransactionStatus.Posted,
            SourceWalletId = sourceWalletId,
            DestinationWalletId = destinationWalletId,
            Amount = decimal.Round(amount, 2, MidpointRounding.ToEven),
            Currency = normalizedCurrency,
            IdempotencyKey = idempotencyKey.Trim(),
            Description = description?.Trim(),
            Metadata = metadata,
            CreatedAt = timestamp,
            UpdatedAt = timestamp
        };
    }
}
