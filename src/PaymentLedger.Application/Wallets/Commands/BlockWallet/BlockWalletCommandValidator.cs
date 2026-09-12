using FluentValidation;

namespace PaymentLedger.Application.Wallets.Commands.BlockWallet;

public class BlockWalletCommandValidator : AbstractValidator<BlockWalletCommand>
{
    public BlockWalletCommandValidator()
    {
        RuleFor(x => x.WalletId)
            .NotEmpty()
            .WithMessage("Wallet ID is required.");
    }
}
