using MediatR;
using PaymentLedger.Application.Transactions.Common;

namespace PaymentLedger.Application.Wallets.Commands.DebitWallet;

public record DebitWalletCommand(
    Guid WalletId,
    decimal Amount,
    string Currency,
    string IdempotencyKey,
    string? Description = null,
    string? Metadata = null) : IRequest<TransactionDto>;
