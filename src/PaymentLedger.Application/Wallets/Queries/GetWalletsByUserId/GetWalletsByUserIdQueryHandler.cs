using MediatR;
using PaymentLedger.Application.Abstractions.Persistence;
using PaymentLedger.Application.Wallets.Common;

namespace PaymentLedger.Application.Wallets.Queries.GetWalletsByUserId;

public class GetWalletsByUserIdQueryHandler : IRequestHandler<GetWalletsByUserIdQuery, IReadOnlyCollection<WalletDto>>
{
    private readonly IWalletRepository _walletRepository;

    public GetWalletsByUserIdQueryHandler(IWalletRepository walletRepository)
    {
        _walletRepository = walletRepository;
    }

    public async Task<IReadOnlyCollection<WalletDto>> Handle(GetWalletsByUserIdQuery request, CancellationToken cancellationToken)
    {
        var wallets = await _walletRepository.ListByUserIdAsync(request.UserId, cancellationToken);

        return wallets.Select(wallet => new WalletDto(
            wallet.Id,
            wallet.UserId,
            wallet.Currency,
            wallet.Balance,
            wallet.Status.ToString(),
            wallet.CreatedAt,
            wallet.UpdatedAt)).ToList();
    }
}
