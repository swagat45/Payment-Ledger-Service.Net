using PaymentLedger.Domain.Entities;

namespace PaymentLedger.Application.Abstractions.Persistence;

public interface ILedgerRepository
{
    Task AddRangeAsync(IEnumerable<LedgerEntry> entries, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<LedgerEntry>> GetByTransactionIdAsync(Guid transactionId, CancellationToken cancellationToken = default);
}
