using FluentAssertions;
using System.Net;
using PaymentLedger.IntegrationTests.Infrastructure;

namespace PaymentLedger.IntegrationTests.HealthChecks;

public sealed class HealthEndpointTests : PostgreSqlIntegrationTestBase
{
    [DockerFact]
    public async Task GetHealth_ReturnsSuccessAfterStartupBootstrap()
    {
        using var client = Host.CreateClient();

        var response = await client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
