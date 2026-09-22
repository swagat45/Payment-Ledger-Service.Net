using PaymentLedger.Business;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using PaymentLedger.Repository.Abstractions;
using PaymentLedger.Business.Common;
using PaymentLedger.Repository;
using PaymentLedger.Repository.Options;
using PaymentLedger.Repository.Persistence;
using PaymentLedger.Repository.Repositories;
using PaymentLedger.Repository.Seeding;

namespace PaymentLedger.IntegrationTests.Infrastructure;

public sealed class InfrastructureRegistrationTests
{
    [Fact]
    public void AddInfrastructure_RegistersCorePersistenceServices()
    {
        var services = new ServiceCollection();
        var configuration = CreateConfiguration();

        services.AddRepository(configuration);
        services.AddBusiness();

        using var serviceProvider = services.BuildServiceProvider();

        Assert.IsType<AppDbContext>(serviceProvider.GetRequiredService<AppDbContext>());
        Assert.IsType<AppDbContext>(serviceProvider.GetRequiredService<IAppDbContext>());
        Assert.IsType<AppDbContext>(serviceProvider.GetRequiredService<IUnitOfWork>());
        Assert.IsType<UserRepository>(serviceProvider.GetRequiredService<IUserRepository>());
        Assert.IsType<WalletRepository>(serviceProvider.GetRequiredService<IWalletRepository>());
        Assert.IsType<TransactionRepository>(serviceProvider.GetRequiredService<ITransactionRepository>());
        Assert.IsType<LedgerRepository>(serviceProvider.GetRequiredService<ILedgerRepository>());
        Assert.IsType<IdempotencyRepository>(serviceProvider.GetRequiredService<IIdempotencyRepository>());
        Assert.IsType<SystemDateTimeProvider>(serviceProvider.GetRequiredService<IDateTimeProvider>());
        Assert.IsType<TreasuryWalletSeeder>(serviceProvider.GetRequiredService<TreasuryWalletSeeder>());
    }

    [Fact]
    public void AddInfrastructure_BindsTreasuryWalletOptions()
    {
        var services = new ServiceCollection();
        var configuration = CreateConfiguration();

        services.AddRepository(configuration);
        services.AddBusiness();

        using var serviceProvider = services.BuildServiceProvider();

        var options = serviceProvider.GetRequiredService<IOptions<TreasuryWalletOptions>>().Value;

        Assert.Equal("treasury@paymentledger.local", options.UserEmail);
        Assert.Equal("Payment Ledger Treasury", options.FullName);
        Assert.Equal(new[] { "USD", "INR" }, options.Currencies);
    }

    [Fact]
    public void AddInfrastructure_RegistersPostgreSqlHealthCheck()
    {
        var services = new ServiceCollection();
        var configuration = CreateConfiguration();

        services.AddRepository(configuration);
        services.AddBusiness();

        using var serviceProvider = services.BuildServiceProvider();

        var healthCheckOptions = serviceProvider
            .GetRequiredService<IOptions<HealthCheckServiceOptions>>()
            .Value;

        Assert.Contains(healthCheckOptions.Registrations, registration => registration.Name == "postgresql");
    }

    private static IConfiguration CreateConfiguration()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Port=5432;Database=payment_ledger;Username=postgres;Password=postgres",
                ["TreasuryWallet:UserEmail"] = "treasury@paymentledger.local",
                ["TreasuryWallet:FullName"] = "Payment Ledger Treasury",
                ["TreasuryWallet:Currencies:0"] = "USD",
                ["TreasuryWallet:Currencies:1"] = "INR"
            })
            .Build();
    }
}
