using Microsoft.EntityFrameworkCore;
using PaymentLedger.Application.Abstractions.Persistence;
using PaymentLedger.Domain.Entities;

namespace PaymentLedger.Infrastructure.Persistence.Repositories;

public sealed class WalletRepository : IWalletRepository
{
    private readonly AppDbContext _dbContext;

    public WalletRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(Wallet wallet, CancellationToken cancellationToken = default)
    {
        await _dbContext.Wallets.AddAsync(wallet, cancellationToken);
    }

    public Task<Wallet?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _dbContext.Wallets.FirstOrDefaultAsync(wallet => wallet.Id == id, cancellationToken);
    }

    public Task<Wallet?> GetByUserAndCurrencyAsync(
        Guid userId,
        string currency,
        CancellationToken cancellationToken = default)
    {
        var normalizedCurrency = currency.Trim().ToUpperInvariant();

        return _dbContext.Wallets.FirstOrDefaultAsync(
            wallet => wallet.UserId == userId && wallet.Currency == normalizedCurrency,
            cancellationToken);
    }

    public async Task<IReadOnlyCollection<Wallet>> ListByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Wallets
            .AsNoTracking()
            .Where(wallet => wallet.UserId == userId)
            .OrderBy(wallet => wallet.Currency)
            .ToListAsync(cancellationToken);
    }
}
