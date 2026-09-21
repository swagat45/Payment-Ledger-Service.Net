using PaymentLedger.Repository.Persistence;
using Microsoft.EntityFrameworkCore;
using PaymentLedger.Repository.Repositories;
using PaymentLedger.Repository.Abstractions;
using PaymentLedger.Repository.Entities;

namespace PaymentLedger.Repository.Repositories;

public sealed class IdempotencyRepository : IIdempotencyRepository
{
    private readonly AppDbContext _dbContext;

    public IdempotencyRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(IdempotencyRequest request, CancellationToken cancellationToken = default)
    {
        await _dbContext.IdempotencyRequests.AddAsync(request, cancellationToken);
    }

    public Task<IdempotencyRequest?> GetByKeyAndEndpointAsync(
        string idempotencyKey,
        string endpoint,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.IdempotencyRequests.FirstOrDefaultAsync(
            request => request.IdempotencyKey == idempotencyKey && request.Endpoint == endpoint,
            cancellationToken);
    }
}
