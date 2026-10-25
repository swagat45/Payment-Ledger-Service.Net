using Microsoft.Extensions.DependencyInjection;
using PaymentLedger.Business.Common;
using PaymentLedger.Business.Events;
using PaymentLedger.Business.Exceptions;
using PaymentLedger.Business.Services;
using PaymentLedger.Repository.Abstractions;

namespace PaymentLedger.Business;

public static class DependencyInjection
{
    public static IServiceCollection AddBusiness(this IServiceCollection services)
    {
        services.AddMemoryCache();
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        services.AddScoped<IIdempotencyService, IdempotencyService>();
        services.AddScoped<IEventPublisher, EventPublisher>();
        
        services.AddSingleton<KafkaIntegrationEventPublisher>();
        services.AddSingleton<LoggingIntegrationEventPublisher>();
        services.AddSingleton<IIntegrationEventPublisher>(sp =>
        {
            var kafkaOptions = sp.GetService<Microsoft.Extensions.Options.IOptions<PaymentLedger.Shared.Options.KafkaOptions>>()?.Value;
            if (kafkaOptions?.Enabled == true)
            {
                return sp.GetRequiredService<KafkaIntegrationEventPublisher>();
            }
            return sp.GetRequiredService<LoggingIntegrationEventPublisher>();
        });

        services.AddScoped<IOutboxDispatcher, OutboxDispatcherService>();
        
        services.AddScoped<UserBusinessService>();
        services.AddScoped<IUserBusinessService>(sp => sp.GetRequiredService<UserBusinessService>());
        services.AddScoped<IUserBusiness>(sp => sp.GetRequiredService<UserBusinessService>());

        services.AddScoped<WalletBusinessService>();
        services.AddScoped<IWalletBusinessService>(sp => sp.GetRequiredService<WalletBusinessService>());
        services.AddScoped<IWalletBusiness>(sp => sp.GetRequiredService<WalletBusinessService>());

        services.AddScoped<TransactionBusinessService>();
        services.AddScoped<ITransactionBusinessService>(sp => sp.GetRequiredService<TransactionBusinessService>());
        services.AddScoped<ITransactionBusiness>(sp => sp.GetRequiredService<TransactionBusinessService>());

        services.AddSingleton<IExceptionToResponseMapper, ExceptionToResponseMapper>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();

        return services;
    }
}
