using Microsoft.EntityFrameworkCore;
using PaymentLedger.Application.Abstractions.Persistence;
using PaymentLedger.Domain.Entities;

namespace PaymentLedger.Infrastructure.Persistence.Repositories;

public sealed class LedgerRepository : ILedgerRepository
{
    private readonly AppDbContext _dbContext;

    public LedgerRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task AddRangeAsync(IEnumerable<LedgerEntry> entries, CancellationToken cancellationToken = default)
    {
        return _dbContext.LedgerEntries.AddRangeAsync(entries, cancellationToken);
    }

    public async Task<IReadOnlyCollection<LedgerEntry>> GetByTransactionIdAsync(
        Guid transactionId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.LedgerEntries
            .AsNoTracking()
            .Where(entry => entry.TransactionId == transactionId)
            .OrderBy(entry => entry.Id)
            .ToListAsync(cancellationToken);
    }
}
