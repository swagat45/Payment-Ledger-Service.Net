namespace PaymentLedger.Application.Wallets.Common;

public record WalletDto(
    Guid WalletId,
    Guid UserId,
    string Currency,
    decimal Balance,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
