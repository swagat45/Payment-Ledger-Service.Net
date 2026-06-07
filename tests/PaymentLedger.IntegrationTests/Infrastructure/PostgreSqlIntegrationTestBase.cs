namespace PaymentLedger.IntegrationTests.Infrastructure;

public abstract class PostgreSqlIntegrationTestBase : IAsyncLifetime
{
    protected PostgreSqlIntegrationTestBase(string environmentName = "IntegrationTests")
    {
        Host = new PostgreSqlIntegrationTestHost(environmentName);
    }

    protected PostgreSqlIntegrationTestHost Host { get; }

    public Task InitializeAsync()
    {
        return Host.InitializeAsync();
    }

    public Task DisposeAsync()
    {
        return Host.DisposeAsync().AsTask();
    }
}
