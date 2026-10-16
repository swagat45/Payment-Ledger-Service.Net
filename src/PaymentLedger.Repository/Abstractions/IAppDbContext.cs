using PaymentLedger.Repository.Entities;

namespace PaymentLedger.Repository.Repositories;

public interface IAppDbContext
{
    IQueryable<User> Users { get; }
    IQueryable<Wallet> Wallets { get; }
    IQueryable<Transaction> Transactions { get; }
    IQueryable<LedgerEntry> LedgerEntries { get; }
    IQueryable<IdempotencyRequest> IdempotencyRequests { get; }
    IQueryable<OutboxMessage> OutboxMessages { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
