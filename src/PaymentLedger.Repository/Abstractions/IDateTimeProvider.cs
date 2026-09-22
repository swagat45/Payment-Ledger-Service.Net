namespace PaymentLedger.Repository.Abstractions;

public interface IDateTimeProvider
{
    DateTimeOffset UtcNow { get; }
}
