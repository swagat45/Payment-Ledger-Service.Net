using FluentValidation;

namespace PaymentLedger.Application.Wallets.Queries.GetWalletsByUserId;

public class GetWalletsByUserIdQueryValidator : AbstractValidator<GetWalletsByUserIdQuery>
{
    public GetWalletsByUserIdQueryValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("User ID is required.");
    }
}
