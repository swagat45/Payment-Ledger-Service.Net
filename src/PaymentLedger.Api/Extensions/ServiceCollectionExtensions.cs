using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

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

        services.AddHealthChecks();
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

        return services;
    }
}
