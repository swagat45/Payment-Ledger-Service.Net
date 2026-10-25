using Microsoft.EntityFrameworkCore;
using PaymentLedger.Repository.Entities;
using PaymentLedger.Repository.Persistence;

namespace PaymentLedger.Repository.Repositories;

public sealed class OutboxRepository : IOutboxRepository
{
    private readonly AppDbContext _dbContext;

    public OutboxRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(OutboxMessage message, CancellationToken cancellationToken = default)
    {
        await _dbContext.OutboxMessages.AddAsync(message, cancellationToken);
    }

    public async Task<IReadOnlyList<OutboxMessage>> GetUnprocessedBatchAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        if (_dbContext.Database.IsNpgsql())
        {
            return await _dbContext.OutboxMessages
                .FromSqlInterpolated($"SELECT * FROM outbox_messages WHERE processed_on_utc IS NULL ORDER BY occurred_on_utc LIMIT {batchSize} FOR UPDATE SKIP LOCKED")
                .ToListAsync(cancellationToken);
        }

        return await _dbContext.OutboxMessages
            .Where(m => m.ProcessedOnUtc == null)
            .OrderBy(m => m.OccurredOnUtc)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
    }

    public void Update(OutboxMessage message)
    {
        _dbContext.OutboxMessages.Update(message);
    }
}
