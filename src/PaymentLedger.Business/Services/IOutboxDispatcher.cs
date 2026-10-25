namespace PaymentLedger.Business.Services;

public interface IOutboxDispatcher
{
    Task<int> ProcessBatchAsync(CancellationToken ct = default);
}
