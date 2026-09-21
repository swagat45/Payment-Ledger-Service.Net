using PaymentLedger.Repository.Persistence;
using PaymentLedger.Repository.Entities;

namespace PaymentLedger.Repository.Repositories;

public interface ILedgerRepository
{
    Task AddRangeAsync(IEnumerable<LedgerEntry> entries, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<LedgerEntry>> GetByTransactionIdAsync(Guid transactionId, CancellationToken cancellationToken = default);
}
