using PaymentLedger.Api.Common;
using PaymentLedger.Api.Filters;
using PaymentLedger.Shared.Context;

namespace PaymentLedger.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApiServices(this IServiceCollection services)
    {
        services.AddControllers(options =>
        {
            options.Filters.Add<ApiResponseCorrelationFilter>();
        });
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();
        services.AddHealthChecks();
        services.AddHttpContextAccessor();
        services.AddScoped<ICorrelationIdProvider, CorrelationIdProvider>();

        return services;
    }
}
