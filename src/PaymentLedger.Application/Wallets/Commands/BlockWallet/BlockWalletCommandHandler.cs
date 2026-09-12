using MediatR;
using PaymentLedger.Application.Abstractions.Common;
using PaymentLedger.Application.Abstractions.Persistence;
using PaymentLedger.Application.Wallets.Common;

namespace PaymentLedger.Application.Wallets.Commands.BlockWallet;

public class BlockWalletCommandHandler : IRequestHandler<BlockWalletCommand, WalletDto?>
{
    private readonly IWalletRepository _walletRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;

    public BlockWalletCommandHandler(
        IWalletRepository walletRepository,
        IUnitOfWork unitOfWork,
        IDateTimeProvider dateTimeProvider)
    {
        _walletRepository = walletRepository;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<WalletDto?> Handle(BlockWalletCommand request, CancellationToken cancellationToken)
    {
        var wallet = await _walletRepository.GetByIdAsync(request.WalletId, cancellationToken);
        if (wallet == null)
        {
            return null;
        }

        wallet.Block(_dateTimeProvider.UtcNow);
        _walletRepository.Update(wallet);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

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
