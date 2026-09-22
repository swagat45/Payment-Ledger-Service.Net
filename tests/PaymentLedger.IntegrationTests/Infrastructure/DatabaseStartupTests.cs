using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PaymentLedger.Repository.Persistence;

namespace PaymentLedger.IntegrationTests.Infrastructure;

public sealed class DatabaseStartupTests : PostgreSqlIntegrationTestBase
{
    [DockerFact]
    public async Task Startup_AppliesInitialMigrationAndSeedsTreasuryData()
    {
        using var client = Host.CreateClient();

        var response = await client.GetAsync("/health");

        response.EnsureSuccessStatusCode();

        await using var scope = Host.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var appliedMigrations = await dbContext.Database.GetAppliedMigrationsAsync();
        var treasuryUsers = await dbContext.Users
            .Where(user => user.Email == "treasury@paymentledger.local")
            .ToListAsync();

        var treasuryWallets = await dbContext.Wallets
            .Where(wallet => wallet.UserId == treasuryUsers.Single().Id)
            .OrderBy(wallet => wallet.Currency)
            .ToListAsync();

        appliedMigrations.Should().Contain(migration => migration.EndsWith("_InitialCreate", StringComparison.Ordinal));
        treasuryUsers.Should().HaveCount(1);
        treasuryWallets.Select(wallet => wallet.Currency).Should().Equal("INR", "USD");
    }

    [DockerFact]
    public async Task Startup_SeedingIsIdempotentAcrossMultipleBoots()
    {
        await using var initialScope = Host.CreateScope();
        var initialDbContext = initialScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var treasuryUser = await initialDbContext.Users
            .SingleAsync(user => user.Email == "treasury@paymentledger.local");

        var initialWalletCount = await initialDbContext.Wallets.CountAsync(wallet => wallet.UserId == treasuryUser.Id);

        using (var nextFactory = Host.CreateFactory())
        using (var nextClient = nextFactory.CreateClient())
        {
            var nextResponse = await nextClient.GetAsync("/health");
            nextResponse.EnsureSuccessStatusCode();
        }

        await using var verificationScope = Host.CreateScope();
        var verificationDbContext = verificationScope.ServiceProvider.GetRequiredService<AppDbContext>();

        var treasuryUsers = await verificationDbContext.Users
            .CountAsync(user => user.Email == "treasury@paymentledger.local");

        var walletCount = await verificationDbContext.Wallets
            .CountAsync(wallet => wallet.UserId == treasuryUser.Id);

        treasuryUsers.Should().Be(1);
        walletCount.Should().Be(initialWalletCount);
    }
}
