using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using PaymentLedger.Application.Behaviors;
using PaymentLedger.Application.Common.Exceptions;

namespace PaymentLedger.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddMediatR(config =>
        {
            config.RegisterServicesFromAssembly(assembly);
            config.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        services.AddValidatorsFromAssembly(assembly);
        services.AddSingleton<IExceptionToResponseMapper, ExceptionToResponseMapper>();

        return services;
    }
}
