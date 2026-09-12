using FluentValidation;

namespace PaymentLedger.Application.Wallets.Queries.GetWalletById;

public class GetWalletByIdQueryValidator : AbstractValidator<GetWalletByIdQuery>
{
    public GetWalletByIdQueryValidator()
    {
        RuleFor(x => x.WalletId)
            .NotEmpty()
            .WithMessage("Wallet ID is required.");
    }
}
