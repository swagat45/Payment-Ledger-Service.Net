using PaymentLedger.Domain.Entities;

namespace PaymentLedger.Application.Abstractions.Persistence;

public interface IAppDbContext
{
    IQueryable<User> Users { get; }
    IQueryable<Wallet> Wallets { get; }
    IQueryable<Transaction> Transactions { get; }
    IQueryable<LedgerEntry> LedgerEntries { get; }
    IQueryable<IdempotencyRequest> IdempotencyRequests { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
