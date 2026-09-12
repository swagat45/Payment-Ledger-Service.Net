using FluentValidation;

namespace PaymentLedger.Application.Wallets.Commands.CloseWallet;

public class CloseWalletCommandValidator : AbstractValidator<CloseWalletCommand>
{
    public CloseWalletCommandValidator()
    {
        RuleFor(x => x.WalletId)
            .NotEmpty()
            .WithMessage("Wallet ID is required.");
    }
}
