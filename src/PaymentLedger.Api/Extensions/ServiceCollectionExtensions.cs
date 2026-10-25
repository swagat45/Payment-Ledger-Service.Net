using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;

using PaymentLedger.Api.BackgroundServices;
using PaymentLedger.Api.Common;
using PaymentLedger.Api.Filters;
using PaymentLedger.Shared.Constants;
using PaymentLedger.Shared.Context;
using PaymentLedger.Shared.Options;

namespace PaymentLedger.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApiServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddControllers(options =>
        {
            options.Filters.Add<ApiResponseCorrelationFilter>();
        });
        services.AddEndpointsApiExplorer();
        
        services.AddSwaggerGen();

        services.AddHealthChecks()
            .AddCheck<RedisHealthCheck>("redis", tags: ["cache", "redis"]);

        services.AddHttpContextAccessor();
        services.AddScoped<ICorrelationIdProvider, CorrelationIdProvider>();
        
        // Add CurrentUserService
        services.AddScoped<ICurrentUserService, CurrentUserService>();

        services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy.AllowAnyOrigin()
                      .AllowAnyHeader()
                      .AllowAnyMethod()
                      .WithExposedHeaders("X-Correlation-ID");
            });
        });

        // Redis Configuration
        var redisConnectionString = configuration.GetConnectionString("Redis");
        if (!string.IsNullOrEmpty(redisConnectionString))
        {
            services.AddSingleton<IConnectionMultiplexer>(sp =>
            {
                var redisOptions = ConfigurationOptions.Parse(redisConnectionString);
                redisOptions.AbortOnConnectFail = false;
                redisOptions.ConnectTimeout = 5000;
                redisOptions.SyncTimeout = 3000;
                return ConnectionMultiplexer.Connect(redisOptions);
            });
        }

        // JWT Configuration
        var jwtOptions = new JwtOptions();
        configuration.GetSection(JwtOptions.SectionName).Bind(jwtOptions);
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtOptions.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SecretKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };
            });

        services.AddAuthorization(options =>
        {
            options.AddPolicy(SecurityConstants.Policies.ReadWallet, policy =>
                policy.RequireAssertion(context =>
                    context.User.HasClaim(c => c.Type == SecurityConstants.ClaimTypes.Scope && c.Value == SecurityConstants.Scopes.ReadWallet) ||
                    context.User.IsInRole(SecurityConstants.Roles.Admin)));

            options.AddPolicy(SecurityConstants.Policies.WriteWallet, policy =>
                policy.RequireAssertion(context =>
                    context.User.HasClaim(c => c.Type == SecurityConstants.ClaimTypes.Scope && c.Value == SecurityConstants.Scopes.WriteWallet) ||
                    context.User.IsInRole(SecurityConstants.Roles.Admin)));

            options.AddPolicy(SecurityConstants.Policies.AdminLedger, policy =>
                policy.RequireAssertion(context =>
                    context.User.HasClaim(c => c.Type == SecurityConstants.ClaimTypes.Scope && c.Value == SecurityConstants.Scopes.AdminLedger) ||
                    context.User.IsInRole(SecurityConstants.Roles.Admin)));
        });

        // Kafka Configuration Options
        services.Configure<KafkaOptions>(configuration.GetSection(KafkaOptions.SectionName));

        // Outbox Configuration & Hosted Worker
        services.Configure<OutboxOptions>(configuration.GetSection(OutboxOptions.SectionName));
        services.AddHostedService<OutboxProcessorBackgroundService>();

        return services;
    }
}

