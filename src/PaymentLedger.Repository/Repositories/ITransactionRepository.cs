using PaymentLedger.Repository.Persistence;
using PaymentLedger.Repository.Entities;

namespace PaymentLedger.Repository.Repositories;

public interface ITransactionRepository
{
    Task AddAsync(Transaction transaction, CancellationToken cancellationToken = default);
    Task<Transaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Transaction?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<Transaction>> ListByWalletIdAsync(Guid walletId, CancellationToken cancellationToken = default);
}
