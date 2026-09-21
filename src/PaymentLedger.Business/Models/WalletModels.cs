namespace PaymentLedger.Business.Models;

// Requests
public record CreateWalletRequest(Guid UserId, string Currency);

public record CreditWalletRequest(
    decimal Amount,
    string Currency,
    string? IdempotencyKey = null,
    string? ReferenceId = null,
    string? Description = null,
    string? Metadata = null);

public record DebitWalletRequest(
    decimal Amount,
    string Currency,
    string? IdempotencyKey = null,
    string? ReferenceId = null,
    string? Description = null,
    string? Metadata = null);

// Responses / View Models / DTOs
public record WalletResponse(
    Guid WalletId,
    Guid UserId,
    string Currency,
    decimal Balance,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public record WalletBalanceResponse(
    Guid WalletId,
    Guid UserId,
    string Currency,
    decimal AvailableBalance,
    string Status,
    DateTimeOffset LastUpdatedAt);

public record TransactionResponse(
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

public record LedgerEntryDto(
    long Id,
    Guid TransactionId,
    Guid WalletId,
    string EntryType,
    decimal Amount,
    string Currency,
    decimal? BalanceAfterTransaction,
    DateTimeOffset CreatedAt);

// Type aliases for seamless compatibility
public record WalletDto(
    Guid WalletId,
    Guid UserId,
    string Currency,
    decimal Balance,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt) : WalletResponse(WalletId, UserId, Currency, Balance, Status, CreatedAt, UpdatedAt);

public record WalletBalanceDto(
    Guid WalletId,
    Guid UserId,
    string Currency,
    decimal AvailableBalance,
    string Status,
    DateTimeOffset LastUpdatedAt) : WalletBalanceResponse(WalletId, UserId, Currency, AvailableBalance, Status, LastUpdatedAt);

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
    DateTimeOffset CreatedAt) : TransactionResponse(TransactionId, TransactionCode, Type, Status, SourceWalletId, DestinationWalletId, Amount, Currency, IdempotencyKey, Description, Metadata, BalanceAfterTransaction, CreatedAt);
