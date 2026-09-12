using MediatR;
using PaymentLedger.Application.Wallets.Common;

namespace PaymentLedger.Application.Wallets.Commands.CreateWallet;

public record CreateWalletCommand(
    Guid UserId,
    string Currency) : IRequest<WalletDto>;
