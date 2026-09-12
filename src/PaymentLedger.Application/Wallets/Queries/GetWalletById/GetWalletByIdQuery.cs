using MediatR;
using PaymentLedger.Application.Wallets.Common;

namespace PaymentLedger.Application.Wallets.Queries.GetWalletById;

public record GetWalletByIdQuery(Guid WalletId) : IRequest<WalletDto?>;
