namespace PaymentLedger.Application.Wallets.Common;

public record WalletBalanceDto(
    Guid WalletId,
    Guid UserId,
    string Currency,
    decimal AvailableBalance,
    string Status,
    DateTimeOffset LastUpdatedAt);
