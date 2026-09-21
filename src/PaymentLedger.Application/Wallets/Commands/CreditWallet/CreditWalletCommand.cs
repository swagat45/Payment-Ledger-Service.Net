using MediatR;
using PaymentLedger.Application.Transactions.Common;

namespace PaymentLedger.Application.Wallets.Commands.CreditWallet;

public record CreditWalletCommand(
    Guid WalletId,
    decimal Amount,
    string Currency,
    string IdempotencyKey,
    string? Description = null,
    string? Metadata = null,
    string? ReferenceId = null) : IRequest<TransactionDto>;
