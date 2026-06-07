using PaymentLedger.Application.Abstractions.Common;

namespace PaymentLedger.Infrastructure.Common;

public sealed class SystemDateTimeProvider : IDateTimeProvider
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
