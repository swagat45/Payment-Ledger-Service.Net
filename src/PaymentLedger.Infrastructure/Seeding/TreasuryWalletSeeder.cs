using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PaymentLedger.Application.Abstractions.Common;
using PaymentLedger.Domain.Entities;
using PaymentLedger.Domain.Enums;
using PaymentLedger.Infrastructure.Options;
using PaymentLedger.Infrastructure.Persistence;

namespace PaymentLedger.Infrastructure.Seeding;

public sealed class TreasuryWalletSeeder
{
    private readonly AppDbContext _dbContext;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly TreasuryWalletOptions _options;

    public TreasuryWalletSeeder(
        AppDbContext dbContext,
        IDateTimeProvider dateTimeProvider,
        IOptions<TreasuryWalletOptions> options)
    {
        _dbContext = dbContext;
        _dateTimeProvider = dateTimeProvider;
        _options = options.Value;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var normalizedEmail = _options.UserEmail.Trim();
        var normalizedFullName = _options.FullName.Trim();
        var currencies = _options.Currencies
            .Where(currency => !string.IsNullOrWhiteSpace(currency))
            .Select(currency => currency.Trim().ToUpperInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (string.IsNullOrWhiteSpace(normalizedEmail) ||
            string.IsNullOrWhiteSpace(normalizedFullName) ||
            currencies.Length == 0)
        {
            return;
        }

        var now = _dateTimeProvider.UtcNow;
        var treasuryUser = await _dbContext.Users
            .FirstOrDefaultAsync(user => user.Email == normalizedEmail, cancellationToken);

        if (treasuryUser is null)
        {
            treasuryUser = new User();
            var userEntry = await _dbContext.Users.AddAsync(treasuryUser, cancellationToken);

            userEntry.Property(user => user.Id).CurrentValue = Guid.NewGuid();
            userEntry.Property(user => user.FullName).CurrentValue = normalizedFullName;
            userEntry.Property(user => user.Email).CurrentValue = normalizedEmail;
            userEntry.Property(user => user.Status).CurrentValue = UserStatus.Active;
            userEntry.Property(user => user.CreatedAt).CurrentValue = now;
            userEntry.Property(user => user.UpdatedAt).CurrentValue = now;
        }

        foreach (var currency in currencies)
        {
            var walletExists = await _dbContext.Wallets.AnyAsync(
                wallet => wallet.UserId == treasuryUser.Id && wallet.Currency == currency,
                cancellationToken);

            if (walletExists)
            {
                continue;
            }

            var wallet = new Wallet();
            var walletEntry = await _dbContext.Wallets.AddAsync(wallet, cancellationToken);

            walletEntry.Property(entity => entity.Id).CurrentValue = Guid.NewGuid();
            walletEntry.Property(entity => entity.UserId).CurrentValue = treasuryUser.Id;
            walletEntry.Property(entity => entity.Currency).CurrentValue = currency;
            walletEntry.Property(entity => entity.Balance).CurrentValue = 0m;
            walletEntry.Property(entity => entity.Status).CurrentValue = WalletStatus.Active;
            walletEntry.Property(entity => entity.CreatedAt).CurrentValue = now;
            walletEntry.Property(entity => entity.UpdatedAt).CurrentValue = now;
        }

        if (_dbContext.ChangeTracker.HasChanges())
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
