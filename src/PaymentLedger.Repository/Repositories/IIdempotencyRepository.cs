using PaymentLedger.Repository.Persistence;
using PaymentLedger.Repository.Entities;

namespace PaymentLedger.Repository.Repositories;

public interface IIdempotencyRepository
{
    Task AddAsync(IdempotencyRequest request, CancellationToken cancellationToken = default);
    Task<IdempotencyRequest?> GetByKeyAndEndpointAsync(
        string idempotencyKey,
        string endpoint,
        CancellationToken cancellationToken = default);
}
