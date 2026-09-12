using MediatR;
using PaymentLedger.Application.Abstractions.Common;
using PaymentLedger.Application.Abstractions.Persistence;
using PaymentLedger.Application.Wallets.Common;
using PaymentLedger.Domain.Entities;
using PaymentLedger.Domain.Enums;
using PaymentLedger.Domain.Exceptions;

namespace PaymentLedger.Application.Wallets.Commands.CreateWallet;

public class CreateWalletCommandHandler : IRequestHandler<CreateWalletCommand, WalletDto>
{
    private readonly IWalletRepository _walletRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeProvider _dateTimeProvider;

    public CreateWalletCommandHandler(
        IWalletRepository walletRepository,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        IDateTimeProvider dateTimeProvider)
    {
        _walletRepository = walletRepository;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<WalletDto> Handle(CreateWalletCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user == null)
        {
            throw new DomainException("User does not exist.");
        }

        if (user.Status != UserStatus.Active)
        {
            throw new DomainException("Cannot create a wallet for an inactive user.");
        }

        var normalizedCurrency = request.Currency.Trim().ToUpperInvariant();
        var existingWallet = await _walletRepository.GetByUserAndCurrencyAsync(
            request.UserId,
            normalizedCurrency,
            cancellationToken);

        if (existingWallet != null)
        {
            throw new DomainException($"A wallet with currency '{normalizedCurrency}' already exists for this user.");
        }

        var wallet = Wallet.Create(request.UserId, normalizedCurrency, _dateTimeProvider.UtcNow);

        await _walletRepository.AddAsync(wallet, cancellationToken);
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
