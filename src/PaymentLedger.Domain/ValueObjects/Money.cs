namespace PaymentLedger.Domain.ValueObjects;

public readonly record struct Money
{
    public decimal Amount { get; }

    public string Currency { get; }
}
