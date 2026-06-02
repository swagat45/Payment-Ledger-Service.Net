namespace PaymentLedger.Application.Abstractions.Common;

public interface IDateTimeProvider
{
    DateTimeOffset UtcNow { get; }
}
