using Microsoft.EntityFrameworkCore;
using PaymentLedger.Application.Abstractions.Persistence;
using PaymentLedger.Domain.Entities;

namespace PaymentLedger.Infrastructure.Persistence.Repositories;

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
