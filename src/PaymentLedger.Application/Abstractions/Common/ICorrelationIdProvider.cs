namespace PaymentLedger.Application.Abstractions.Common;

public interface ICorrelationIdProvider
{
    string CorrelationId { get; }
}
