using PaymentLedger.Repository.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PaymentLedger.Repository.Repositories;
using PaymentLedger.Repository.Abstractions;
using PaymentLedger.Repository.Entities;
using PaymentLedger.Repository.Options;

namespace PaymentLedger.Repository.Repositories;

public sealed class WalletRepository : IWalletRepository
{
    private readonly AppDbContext _dbContext;
    private readonly TreasuryWalletOptions _treasuryOptions;

    public WalletRepository(AppDbContext dbContext, IOptions<TreasuryWalletOptions> treasuryOptions)
    {
        _dbContext = dbContext;
        _treasuryOptions = treasuryOptions.Value;
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

    public void Update(Wallet wallet)
    {
        _dbContext.Wallets.Update(wallet);
    }

    public Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _dbContext.Wallets.AnyAsync(wallet => wallet.Id == id, cancellationToken);
    }

    public async Task<Wallet?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Wallets
            .FromSqlInterpolated($"SELECT * FROM wallets WHERE id = {id} FOR UPDATE")
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<Wallet?> GetTreasuryWalletAsync(string currency, CancellationToken cancellationToken = default)
    {
        var normalizedCurrency = currency.Trim().ToUpperInvariant();
        var treasuryEmail = _treasuryOptions.UserEmail.Trim();

        return await _dbContext.Wallets
            .FirstOrDefaultAsync(w =>
                _dbContext.Users.Any(u => u.Id == w.UserId && u.Email == treasuryEmail)
                && w.Currency == normalizedCurrency,
                cancellationToken);
    }
}
