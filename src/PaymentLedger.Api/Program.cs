using Microsoft.EntityFrameworkCore;
using PaymentLedger.Api.Extensions;
using PaymentLedger.Api.Middleware;
using PaymentLedger.Application;
using PaymentLedger.Infrastructure;
using PaymentLedger.Infrastructure.Persistence;
using PaymentLedger.Infrastructure.Seeding;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{CorrelationId}] {Message:lj}{NewLine}{Exception}")
        .WriteTo.File(
            path: "logs/payment-ledger-.log",
            rollingInterval: RollingInterval.Day,
            outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] [{CorrelationId}] {Message:lj}{NewLine}{Exception}");
});

var disableHttpsRedirection = builder.Configuration.GetValue<bool>("DisableHttpsRedirection");

builder.Services.AddApiServices();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<GlobalExceptionHandlerMiddleware>();
app.UseSerilogRequestLogging();

await using (var scope = app.Services.CreateAsyncScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await dbContext.Database.MigrateAsync();

    var treasuryWalletSeeder = scope.ServiceProvider.GetRequiredService<TreasuryWalletSeeder>();
    await treasuryWalletSeeder.SeedAsync();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (!disableHttpsRedirection)
{
    app.UseHttpsRedirection();
}

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

public partial class Program;

