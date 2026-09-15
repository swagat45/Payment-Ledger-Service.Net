using FluentValidation;
using System.Text.Json;

namespace PaymentLedger.Application.Wallets.Commands.DebitWallet;

public class DebitWalletCommandValidator : AbstractValidator<DebitWalletCommand>
{
    public DebitWalletCommandValidator()
    {
        RuleFor(x => x.WalletId)
            .NotEmpty()
            .WithMessage("Wallet ID is required.");

        RuleFor(x => x.Amount)
            .GreaterThan(0)
            .WithMessage("Debit amount must be greater than zero.");

        RuleFor(x => x.Currency)
            .NotEmpty()
            .WithMessage("Currency is required.")
            .Length(3)
            .WithMessage("Currency must be a 3-letter code.")
            .Matches("^[a-zA-Z]{3}$")
            .WithMessage("Currency must contain only letters.");

        RuleFor(x => x.IdempotencyKey)
            .NotEmpty()
            .WithMessage("Idempotency key is required.")
            .MaximumLength(200)
            .WithMessage("Idempotency key must not exceed 200 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(500)
            .WithMessage("Description must not exceed 500 characters.")
            .When(x => !string.IsNullOrWhiteSpace(x.Description));

        RuleFor(x => x.Metadata)
            .Must(BeValidJson)
            .WithMessage("Metadata must be valid JSON.")
            .When(x => !string.IsNullOrWhiteSpace(x.Metadata));
    }

    private static bool BeValidJson(string? metadata)
    {
        if (string.IsNullOrWhiteSpace(metadata))
        {
            return true;
        }

        try
        {
            using var doc = JsonDocument.Parse(metadata);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
