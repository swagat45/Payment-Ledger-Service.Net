using PaymentLedger.Domain.Entities;

namespace PaymentLedger.Application.Abstractions.Persistence;

public interface ITransactionRepository
{
    Task AddAsync(Transaction transaction, CancellationToken cancellationToken = default);
    Task<Transaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Transaction?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<Transaction>> ListByWalletIdAsync(Guid walletId, CancellationToken cancellationToken = default);
}
