using HealthChecks.NpgSql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PaymentLedger.Application.Abstractions.Common;
using PaymentLedger.Application.Abstractions.Persistence;
using PaymentLedger.Infrastructure.Common;
using PaymentLedger.Infrastructure.Options;
using PaymentLedger.Infrastructure.Persistence;
using PaymentLedger.Infrastructure.Persistence.Repositories;
using PaymentLedger.Infrastructure.Seeding;

namespace PaymentLedger.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' was not found.");

        var treasurySection = configuration.GetSection(TreasuryWalletOptions.SectionName);

        services.Configure<TreasuryWalletOptions>(options =>
        {
            options.UserEmail = treasurySection["UserEmail"] ?? string.Empty;
            options.FullName = treasurySection["FullName"] ?? string.Empty;
            options.Currencies = treasurySection
                .GetSection("Currencies")
                .GetChildren()
                .Select(section => section.Value ?? string.Empty)
                .ToArray();
        });

        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
            });
        });

        services.AddScoped<IAppDbContext>(serviceProvider => serviceProvider.GetRequiredService<AppDbContext>());
        services.AddScoped<IUnitOfWork>(serviceProvider => serviceProvider.GetRequiredService<AppDbContext>());
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IWalletRepository, WalletRepository>();
        services.AddScoped<ITransactionRepository, TransactionRepository>();
        services.AddScoped<ILedgerRepository, LedgerRepository>();
        services.AddScoped<IIdempotencyRepository, IdempotencyRepository>();
        services.AddMemoryCache();
        services.AddScoped<IIdempotencyService, IdempotencyService>();
        services.AddScoped<TreasuryWalletSeeder>();
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();

        services.AddHealthChecks()
            .AddNpgSql(connectionString, name: "postgresql");

        return services;
    }
}
