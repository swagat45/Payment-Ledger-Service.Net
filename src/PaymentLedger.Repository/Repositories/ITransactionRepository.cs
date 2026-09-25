using PaymentLedger.Repository.Entities;
using PaymentLedger.Repository.Enums;

namespace PaymentLedger.Repository.Repositories;

public interface ITransactionRepository
{
    Task AddAsync(Transaction transaction, CancellationToken cancellationToken = default);
    Task<Transaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Transaction?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<Transaction>> ListByWalletIdAsync(Guid walletId, CancellationToken cancellationToken = default);

    Task<(Transaction Transaction, IReadOnlyCollection<LedgerEntry> Entries, Transaction? ReferenceTransaction)?> GetByIdWithEntriesAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<Transaction>> GetRefundsForTransactionAsync(
        Guid originalTransactionId,
        CancellationToken cancellationToken = default);

    Task<decimal> GetTotalRefundedAmountAsync(
        Guid originalTransactionId,
        CancellationToken cancellationToken = default);

    Task<(IReadOnlyCollection<Transaction> Items, long TotalCount)> GetPagedByWalletIdAsync(
        Guid walletId,
        int pageNumber,
        int pageSize,
        TransactionType? type = null,
        TransactionStatus? status = null,
        DateTimeOffset? fromDate = null,
        DateTimeOffset? toDate = null,
        CancellationToken cancellationToken = default);
}
