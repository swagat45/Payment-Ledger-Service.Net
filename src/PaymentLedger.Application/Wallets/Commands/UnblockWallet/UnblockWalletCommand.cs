using MediatR;
using PaymentLedger.Application.Wallets.Common;

namespace PaymentLedger.Application.Wallets.Commands.UnblockWallet;

public record UnblockWalletCommand(Guid WalletId) : IRequest<WalletDto?>;
