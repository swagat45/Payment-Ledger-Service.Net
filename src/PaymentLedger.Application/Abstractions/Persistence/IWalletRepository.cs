using PaymentLedger.Domain.Entities;

namespace PaymentLedger.Application.Abstractions.Persistence;

public interface IWalletRepository
{
    Task AddAsync(Wallet wallet, CancellationToken cancellationToken = default);
    Task<Wallet?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Wallet?> GetByUserAndCurrencyAsync(Guid userId, string currency, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<Wallet>> ListByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    void Update(Wallet wallet);
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);
}
