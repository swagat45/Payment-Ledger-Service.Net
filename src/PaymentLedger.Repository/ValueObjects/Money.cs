using System.Globalization;

namespace PaymentLedger.Repository.ValueObjects;

public readonly record struct Money
{
    public Money(decimal amount, string currency)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Amount must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(currency))
        {
            throw new ArgumentException("Currency is required.", nameof(currency));
        }

        var normalizedCurrency = currency.Trim().ToUpperInvariant();

        if (normalizedCurrency.Length != 3)
        {
            throw new ArgumentException("Currency must be a 3-letter uppercase ISO-style code.", nameof(currency));
        }

        if (!normalizedCurrency.All(char.IsLetter))
        {
            throw new ArgumentException("Currency must contain only letters.", nameof(currency));
        }

        Amount = decimal.Round(amount, 2, MidpointRounding.ToEven);
        Currency = normalizedCurrency;
    }

    public decimal Amount { get; }

    public string Currency { get; }

    public override string ToString()
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{Currency} {Amount:0.00}");
    }
}
