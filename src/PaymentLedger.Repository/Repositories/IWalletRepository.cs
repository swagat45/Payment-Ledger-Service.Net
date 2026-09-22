using PaymentLedger.Repository.Persistence;
using PaymentLedger.Repository.Entities;

namespace PaymentLedger.Repository.Repositories;

public interface IWalletRepository
{
    Task AddAsync(Wallet wallet, CancellationToken cancellationToken = default);
    Task<Wallet?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Wallet?> GetByUserAndCurrencyAsync(Guid userId, string currency, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<Wallet>> ListByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    void Update(Wallet wallet);
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Wallet?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Wallet?> GetTreasuryWalletAsync(string currency, CancellationToken cancellationToken = default);
}
