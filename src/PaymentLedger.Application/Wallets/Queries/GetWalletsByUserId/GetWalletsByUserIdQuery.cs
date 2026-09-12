using MediatR;
using PaymentLedger.Application.Wallets.Common;

namespace PaymentLedger.Application.Wallets.Queries.GetWalletsByUserId;

public record GetWalletsByUserIdQuery(Guid UserId) : IRequest<IReadOnlyCollection<WalletDto>>;
