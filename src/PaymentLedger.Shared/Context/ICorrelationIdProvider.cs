namespace PaymentLedger.Shared.Context;

public interface ICorrelationIdProvider
{
    string CorrelationId { get; }
}
