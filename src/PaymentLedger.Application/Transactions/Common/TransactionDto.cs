namespace PaymentLedger.Application.Transactions.Common;

public record TransactionDto(
    Guid TransactionId,
    string TransactionCode,
    string Type,
    string Status,
    Guid? SourceWalletId,
    Guid? DestinationWalletId,
    decimal Amount,
    string Currency,
    string IdempotencyKey,
    string? Description,
    string? Metadata,
    decimal? BalanceAfterTransaction,
    DateTimeOffset CreatedAt);
