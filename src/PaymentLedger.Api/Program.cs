using Microsoft.EntityFrameworkCore;
using PaymentLedger.Api.Extensions;
using PaymentLedger.Application;
using PaymentLedger.Infrastructure;
using PaymentLedger.Infrastructure.Persistence;
using PaymentLedger.Infrastructure.Seeding;

var builder = WebApplication.CreateBuilder(args);
var disableHttpsRedirection = builder.Configuration.GetValue<bool>("DisableHttpsRedirection");

builder.Services.AddApiServices();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

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
