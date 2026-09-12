using MediatR;
using PaymentLedger.Application.Wallets.Common;

namespace PaymentLedger.Application.Wallets.Commands.CloseWallet;

public record CloseWalletCommand(Guid WalletId) : IRequest<WalletDto?>;
