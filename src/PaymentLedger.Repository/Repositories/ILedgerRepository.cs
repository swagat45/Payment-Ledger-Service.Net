using PaymentLedger.Repository.Entities;

namespace PaymentLedger.Repository.Repositories;

public interface ILedgerRepository
{
    Task AddRangeAsync(IEnumerable<LedgerEntry> entries, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<LedgerEntry>> GetByTransactionIdAsync(Guid transactionId, CancellationToken cancellationToken = default);

    Task<decimal> GetWalletLedgerSumAsync(Guid walletId, CancellationToken cancellationToken = default);

    Task<(decimal TotalDebits, decimal TotalCredits)> GetTransactionLedgerSumAsync(
        Guid transactionId,
        CancellationToken cancellationToken = default);

    Task<SystemLedgerAuditResult> GetSystemLedgerDiscrepanciesAsync(CancellationToken cancellationToken = default);
}
