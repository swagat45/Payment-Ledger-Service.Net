using PaymentLedger.Repository.Persistence;
using Microsoft.EntityFrameworkCore;
using PaymentLedger.Repository.Repositories;
using PaymentLedger.Repository.Abstractions;
using PaymentLedger.Repository.Entities;

namespace PaymentLedger.Repository.Repositories;

public sealed class TransactionRepository : ITransactionRepository
{
    private readonly AppDbContext _dbContext;

    public TransactionRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(Transaction transaction, CancellationToken cancellationToken = default)
    {
        await _dbContext.Transactions.AddAsync(transaction, cancellationToken);
    }

    public Task<Transaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _dbContext.Transactions.FirstOrDefaultAsync(transaction => transaction.Id == id, cancellationToken);
    }

    public Task<Transaction?> GetByIdempotencyKeyAsync(
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.Transactions.FirstOrDefaultAsync(
            transaction => transaction.IdempotencyKey == idempotencyKey,
            cancellationToken);
    }

    public async Task<IReadOnlyCollection<Transaction>> ListByWalletIdAsync(
        Guid walletId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Transactions
            .AsNoTracking()
            .Where(transaction => transaction.SourceWalletId == walletId || transaction.DestinationWalletId == walletId)
            .OrderByDescending(transaction => transaction.CreatedAt)
            .ToListAsync(cancellationToken);
    }
}
