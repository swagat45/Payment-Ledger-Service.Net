using MediatR;
using PaymentLedger.Application.Wallets.Common;

namespace PaymentLedger.Application.Wallets.Commands.BlockWallet;

public record BlockWalletCommand(Guid WalletId) : IRequest<WalletDto?>;
