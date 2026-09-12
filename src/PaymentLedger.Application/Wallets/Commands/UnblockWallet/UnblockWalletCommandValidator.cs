using FluentValidation;

namespace PaymentLedger.Application.Wallets.Commands.UnblockWallet;

public class UnblockWalletCommandValidator : AbstractValidator<UnblockWalletCommand>
{
    public UnblockWalletCommandValidator()
    {
        RuleFor(x => x.WalletId)
            .NotEmpty()
            .WithMessage("Wallet ID is required.");
    }
}
