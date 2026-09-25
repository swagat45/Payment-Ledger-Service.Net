using Microsoft.EntityFrameworkCore;
using PaymentLedger.Repository.Entities;
using PaymentLedger.Repository.Enums;
using PaymentLedger.Repository.Persistence;

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

    public async Task<(Transaction Transaction, IReadOnlyCollection<LedgerEntry> Entries, Transaction? ReferenceTransaction)?> GetByIdWithEntriesAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var transaction = await _dbContext.Transactions
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

        if (transaction == null)
        {
            return null;
        }

        var entries = await _dbContext.LedgerEntries
            .AsNoTracking()
            .Where(e => e.TransactionId == id)
            .OrderBy(e => e.Id)
            .ToListAsync(cancellationToken);

        Transaction? referenceTransaction = null;
        if (transaction.ReferenceTransactionId.HasValue)
        {
            referenceTransaction = await _dbContext.Transactions
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == transaction.ReferenceTransactionId.Value, cancellationToken);
        }

        return (transaction, entries, referenceTransaction);
    }

    public async Task<IReadOnlyCollection<Transaction>> GetRefundsForTransactionAsync(
        Guid originalTransactionId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Transactions
            .AsNoTracking()
            .Where(t => t.ReferenceTransactionId == originalTransactionId && t.Status == TransactionStatus.Posted)
            .OrderBy(t => t.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<decimal> GetTotalRefundedAmountAsync(
        Guid originalTransactionId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Transactions
            .AsNoTracking()
            .Where(t => t.ReferenceTransactionId == originalTransactionId &&
                        t.Type == TransactionType.Refund &&
                        t.Status != TransactionStatus.Failed)
            .SumAsync(t => (decimal?)t.Amount, cancellationToken) ?? 0m;
    }

    public async Task<(IReadOnlyCollection<Transaction> Items, long TotalCount)> GetPagedByWalletIdAsync(
        Guid walletId,
        int pageNumber,
        int pageSize,
        TransactionType? type = null,
        TransactionStatus? status = null,
        DateTimeOffset? fromDate = null,
        DateTimeOffset? toDate = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Transactions
            .AsNoTracking()
            .Where(t => t.SourceWalletId == walletId || t.DestinationWalletId == walletId);

        if (type.HasValue)
        {
            query = query.Where(t => t.Type == type.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(t => t.Status == status.Value);
        }

        if (fromDate.HasValue)
        {
            query = query.Where(t => t.CreatedAt >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(t => t.CreatedAt <= toDate.Value);
        }

        var totalCount = await query.LongCountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }
}
