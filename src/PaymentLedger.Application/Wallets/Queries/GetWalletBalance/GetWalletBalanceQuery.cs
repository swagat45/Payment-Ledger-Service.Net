using MediatR;
using PaymentLedger.Application.Wallets.Common;

namespace PaymentLedger.Application.Wallets.Queries.GetWalletBalance;

public record GetWalletBalanceQuery(Guid WalletId) : IRequest<WalletBalanceDto?>;
