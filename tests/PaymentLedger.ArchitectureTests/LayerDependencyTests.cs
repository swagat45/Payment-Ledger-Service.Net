using NetArchTest.Rules;
using PaymentLedger.Application;
using PaymentLedger.Domain.Entities;
using PaymentLedger.Infrastructure;
using PaymentLedger.Shared.Constants;
using System.Reflection;

namespace PaymentLedger.ArchitectureTests;

public sealed class LayerDependencyTests
{
    private static readonly Assembly DomainAssembly = typeof(Wallet).Assembly;
    private static readonly Assembly ApplicationAssembly = typeof(PaymentLedger.Application.DependencyInjection).Assembly;
    private static readonly Assembly InfrastructureAssembly = typeof(PaymentLedger.Infrastructure.DependencyInjection).Assembly;
    private static readonly Assembly SharedAssembly = typeof(ErrorCodes).Assembly;
    private static readonly Assembly ApiAssembly = typeof(Program).Assembly;

    [Fact]
    public void Domain_ShouldNotDependOnApplicationInfrastructureOrApi()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot()
            .HaveDependencyOnAny("PaymentLedger.Application", "PaymentLedger.Infrastructure", "PaymentLedger.Api")
            .GetResult();

        AssertArchitecture(result);
    }

    [Fact]
    public void Application_ShouldNotDependOnInfrastructureOrApi()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot()
            .HaveDependencyOnAny("PaymentLedger.Infrastructure", "PaymentLedger.Api")
            .GetResult();

        AssertArchitecture(result);
    }

    [Fact]
    public void Infrastructure_ShouldNotDependOnApi()
    {
        var result = Types.InAssembly(InfrastructureAssembly)
            .ShouldNot()
            .HaveDependencyOn("PaymentLedger.Api")
            .GetResult();

        AssertArchitecture(result);
    }

    [Fact]
    public void Shared_ShouldStayIndependentOfSolutionLayers()
    {
        var result = Types.InAssembly(SharedAssembly)
            .ShouldNot()
            .HaveDependencyOnAny("PaymentLedger.Api", "PaymentLedger.Application", "PaymentLedger.Domain", "PaymentLedger.Infrastructure")
            .GetResult();

        AssertArchitecture(result);
    }

    [Fact]
    public void Api_ShouldNotDirectlyDependOnDomain()
    {
        var result = Types.InAssembly(ApiAssembly)
            .ShouldNot()
            .HaveDependencyOn("PaymentLedger.Domain")
            .GetResult();

        AssertArchitecture(result);
    }

    private static void AssertArchitecture(TestResult result)
    {
        var failingTypeNames = result.FailingTypeNames ?? [];

        Assert.True(
            result.IsSuccessful,
            $"Architecture assertion failed for types: {string.Join(", ", failingTypeNames)}");
    }
}
