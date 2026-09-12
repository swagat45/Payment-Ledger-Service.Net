using MediatR;
using PaymentLedger.Application.Abstractions.Persistence;
using PaymentLedger.Application.Wallets.Common;

namespace PaymentLedger.Application.Wallets.Queries.GetWalletById;

public class GetWalletByIdQueryHandler : IRequestHandler<GetWalletByIdQuery, WalletDto?>
{
    private readonly IWalletRepository _walletRepository;

    public GetWalletByIdQueryHandler(IWalletRepository walletRepository)
    {
        _walletRepository = walletRepository;
    }

    public async Task<WalletDto?> Handle(GetWalletByIdQuery request, CancellationToken cancellationToken)
    {
        var wallet = await _walletRepository.GetByIdAsync(request.WalletId, cancellationToken);
        if (wallet == null)
        {
            return null;
        }

        return new WalletDto(
            wallet.Id,
            wallet.UserId,
            wallet.Currency,
            wallet.Balance,
            wallet.Status.ToString(),
            wallet.CreatedAt,
            wallet.UpdatedAt);
    }
}
