using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Respawn;
using Respawn.Graph;
using Testcontainers.PostgreSql;

namespace PaymentLedger.IntegrationTests.Infrastructure;

public sealed class PostgreSqlIntegrationTestHost : IAsyncDisposable
{
    private const string DefaultConnectionEnvironmentVariable = "ConnectionStrings__DefaultConnection";
    private const string TreasuryUserEmailEnvironmentVariable = "TreasuryWallet__UserEmail";
    private const string TreasuryFullNameEnvironmentVariable = "TreasuryWallet__FullName";
    private const string TreasuryCurrency0EnvironmentVariable = "TreasuryWallet__Currencies__0";
    private const string TreasuryCurrency1EnvironmentVariable = "TreasuryWallet__Currencies__1";
    private const string DisableHttpsRedirectionEnvironmentVariable = "DisableHttpsRedirection";

    private readonly string _environmentName;
    private PostgreSqlContainer? _postgresContainer;
    private PaymentLedgerWebApplicationFactory? _factory;
    private Respawner? _respawner;

    public PostgreSqlIntegrationTestHost(string environmentName = "IntegrationTests")
    {
        _environmentName = environmentName;
    }

    public string ConnectionString => _postgresContainer?.GetConnectionString()
        ?? throw new InvalidOperationException("The PostgreSQL test container has not been initialized.");

    public async Task InitializeAsync()
    {
        _postgresContainer = new PostgreSqlBuilder("postgres:17-alpine")
            .WithDatabase("payment_ledger_tests")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

        await _postgresContainer.StartAsync();

        Environment.SetEnvironmentVariable(DefaultConnectionEnvironmentVariable, ConnectionString);
        Environment.SetEnvironmentVariable(TreasuryUserEmailEnvironmentVariable, "treasury@paymentledger.local");
        Environment.SetEnvironmentVariable(TreasuryFullNameEnvironmentVariable, "Payment Ledger Treasury");
        Environment.SetEnvironmentVariable(TreasuryCurrency0EnvironmentVariable, "USD");
        Environment.SetEnvironmentVariable(TreasuryCurrency1EnvironmentVariable, "INR");
        Environment.SetEnvironmentVariable(DisableHttpsRedirectionEnvironmentVariable, "true");

        _factory = CreateFactoryCore(_environmentName);

        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/health");
        response.EnsureSuccessStatusCode();

        await using var connection = await CreateOpenConnectionAsync();
        _respawner = await Respawner.CreateAsync(
            connection,
            new RespawnerOptions
            {
                DbAdapter = DbAdapter.Postgres,
                SchemasToInclude = ["public"],
                TablesToIgnore = [new Table("__EFMigrationsHistory")]
            });
    }

    public HttpClient CreateClient()
    {
        return (_factory ?? throw new InvalidOperationException("The test host has not been initialized.")).CreateClient();
    }

    public WebApplicationFactory<Program> CreateFactory(string? environmentName = null)
    {
        return CreateFactoryCore(environmentName ?? _environmentName);
    }

    public AsyncServiceScope CreateScope()
    {
        return (_factory ?? throw new InvalidOperationException("The test host has not been initialized."))
            .Services
            .CreateAsyncScope();
    }

    public async Task ResetDatabaseAsync()
    {
        if (_respawner is null)
        {
            throw new InvalidOperationException("The database respawner has not been initialized.");
        }

        await using var connection = await CreateOpenConnectionAsync();
        await _respawner.ResetAsync(connection);
    }

    public async ValueTask DisposeAsync()
    {
        _factory?.Dispose();

        Environment.SetEnvironmentVariable(DefaultConnectionEnvironmentVariable, null);
        Environment.SetEnvironmentVariable(TreasuryUserEmailEnvironmentVariable, null);
        Environment.SetEnvironmentVariable(TreasuryFullNameEnvironmentVariable, null);
        Environment.SetEnvironmentVariable(TreasuryCurrency0EnvironmentVariable, null);
        Environment.SetEnvironmentVariable(TreasuryCurrency1EnvironmentVariable, null);
        Environment.SetEnvironmentVariable(DisableHttpsRedirectionEnvironmentVariable, null);

        if (_postgresContainer is not null)
        {
            await _postgresContainer.DisposeAsync();
        }
    }

    private PaymentLedgerWebApplicationFactory CreateFactoryCore(string environmentName)
    {
        return new PaymentLedgerWebApplicationFactory(ConnectionString, environmentName);
    }

    private async Task<NpgsqlConnection> CreateOpenConnectionAsync()
    {
        var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        return connection;
    }

    private sealed class PaymentLedgerWebApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly string _connectionString;
        private readonly string _environmentName;

        public PaymentLedgerWebApplicationFactory(string connectionString, string environmentName)
        {
            _connectionString = connectionString;
            _environmentName = environmentName;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(_environmentName);

            builder.ConfigureAppConfiguration((_, configurationBuilder) =>
            {
                configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = _connectionString,
                    ["TreasuryWallet:UserEmail"] = "treasury@paymentledger.local",
                    ["TreasuryWallet:FullName"] = "Payment Ledger Treasury",
                    ["TreasuryWallet:Currencies:0"] = "USD",
                    ["TreasuryWallet:Currencies:1"] = "INR",
                    ["DisableHttpsRedirection"] = "true"
                });
            });
        }
    }
}
