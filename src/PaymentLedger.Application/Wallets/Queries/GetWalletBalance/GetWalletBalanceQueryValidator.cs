using FluentValidation;

namespace PaymentLedger.Application.Wallets.Queries.GetWalletBalance;

public class GetWalletBalanceQueryValidator : AbstractValidator<GetWalletBalanceQuery>
{
    public GetWalletBalanceQueryValidator()
    {
        RuleFor(x => x.WalletId)
            .NotEmpty()
            .WithMessage("Wallet ID is required.");
    }
}
