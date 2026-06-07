using FluentAssertions;
using System.Net;
using PaymentLedger.IntegrationTests.Infrastructure;

namespace PaymentLedger.IntegrationTests.Swagger;

public sealed class SwaggerEndpointTests : PostgreSqlIntegrationTestBase
{
    public SwaggerEndpointTests()
        : base("Development")
    {
    }

    [DockerFact]
    public async Task GetSwaggerJson_ReturnsOpenApiDocumentInDevelopment()
    {
        using var client = Host.CreateClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json");
        var content = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        content.Should().Contain("\"openapi\"", Exactly.Once());
    }
}
