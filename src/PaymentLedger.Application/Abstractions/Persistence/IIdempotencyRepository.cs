using PaymentLedger.Domain.Entities;

namespace PaymentLedger.Application.Abstractions.Persistence;

public interface IIdempotencyRepository
{
    Task AddAsync(IdempotencyRequest request, CancellationToken cancellationToken = default);
    Task<IdempotencyRequest?> GetByKeyAndEndpointAsync(
        string idempotencyKey,
        string endpoint,
        CancellationToken cancellationToken = default);
}
