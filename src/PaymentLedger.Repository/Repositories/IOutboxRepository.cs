using PaymentLedger.Repository.Entities;

namespace PaymentLedger.Repository.Repositories;

public interface IOutboxRepository
{
    Task AddAsync(OutboxMessage message, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OutboxMessage>> GetUnprocessedBatchAsync(int batchSize, CancellationToken cancellationToken = default);

    void Update(OutboxMessage message);
}
