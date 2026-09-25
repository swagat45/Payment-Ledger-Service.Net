using PaymentLedger.Repository.Enums;
using PaymentLedger.Repository.Exceptions;
using PaymentLedger.Shared.Constants;

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

    public static Transaction CreateRefund(
        Guid originalTransactionId,
        Guid sourceWalletId,
        Guid destinationWalletId,
        decimal amount,
        string currency,
        string idempotencyKey,
        DateTimeOffset timestamp,
        string? description = null,
        string? metadata = null)
    {
        if (originalTransactionId == Guid.Empty)
        {
            throw new DomainException("Original transaction ID is required.");
        }

        if (sourceWalletId == Guid.Empty)
        {
            throw new DomainException("Source wallet ID is required.");
        }

        if (destinationWalletId == Guid.Empty)
        {
            throw new DomainException("Destination wallet ID is required.");
        }

        if (amount <= 0)
        {
            throw new DomainException("Refund amount must be greater than zero.", ErrorCodes.InvalidAmount);
        }

        if (string.IsNullOrWhiteSpace(currency))
        {
            throw new DomainException("Currency is required.", ErrorCodes.InvalidCurrency);
        }

        var normalizedCurrency = currency.Trim().ToUpperInvariant();
        if (normalizedCurrency.Length != 3 || !normalizedCurrency.All(char.IsLetter))
        {
            throw new DomainException("Currency must be a valid 3-letter code.", ErrorCodes.InvalidCurrency);
        }

        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            throw new DomainException("Idempotency key is required.", ErrorCodes.IdempotencyKeyRequired);
        }

        var codeSuffix = Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();
        var transactionCode = $"TXN-RF-{timestamp:yyyyMMddHHmmss}-{codeSuffix}";

        return new Transaction
        {
            Id = Guid.NewGuid(),
            TransactionCode = transactionCode,
            Type = TransactionType.Refund,
            Status = TransactionStatus.Posted,
            SourceWalletId = sourceWalletId,
            DestinationWalletId = destinationWalletId,
            Amount = decimal.Round(amount, 2, MidpointRounding.ToEven),
            Currency = normalizedCurrency,
            IdempotencyKey = idempotencyKey.Trim(),
            ReferenceTransactionId = originalTransactionId,
            Description = description?.Trim(),
            Metadata = metadata,
            CreatedAt = timestamp,
            UpdatedAt = timestamp
        };
    }

    public void MarkRefunded(DateTimeOffset timestamp)
    {
        if (Status != TransactionStatus.Posted && Status != TransactionStatus.PartiallyRefunded)
        {
            throw new DomainException(
                $"Cannot refund a transaction with status '{Status}'. Only posted or partially refunded transactions can be refunded.",
                ErrorCodes.TransactionNotRefundable);
        }

        Status = TransactionStatus.Refunded;
        UpdatedAt = timestamp;
    }

    public void MarkPartiallyRefunded(DateTimeOffset timestamp)
    {
        if (Status != TransactionStatus.Posted && Status != TransactionStatus.PartiallyRefunded)
        {
            throw new DomainException(
                $"Cannot partially refund a transaction with status '{Status}'. Only posted or partially refunded transactions can be partially refunded.",
                ErrorCodes.TransactionNotRefundable);
        }

        Status = TransactionStatus.PartiallyRefunded;
        UpdatedAt = timestamp;
    }
}
