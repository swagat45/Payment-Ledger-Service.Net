using PaymentLedger.Repository.Abstractions;

namespace PaymentLedger.Business.Common;

public sealed class SystemDateTimeProvider : IDateTimeProvider
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
