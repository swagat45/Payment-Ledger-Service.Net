using FluentValidation;

namespace PaymentLedger.Application.Wallets.Commands.CreateWallet;

public class CreateWalletCommandValidator : AbstractValidator<CreateWalletCommand>
{
    public CreateWalletCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("User ID is required.");

        RuleFor(x => x.Currency)
            .NotEmpty()
            .WithMessage("Currency is required.")
            .Length(3)
            .WithMessage("Currency must be a 3-letter code.")
            .Matches("^[a-zA-Z]{3}$")
            .WithMessage("Currency must contain only letters.");
    }
}
