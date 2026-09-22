using NetArchTest.Rules;
using PaymentLedger.Business;
using PaymentLedger.Repository.Entities;
using PaymentLedger.Shared.Constants;
using System.Reflection;

namespace PaymentLedger.ArchitectureTests;

public sealed class LayerDependencyTests
{
    private static readonly Assembly RepositoryAssembly = typeof(Wallet).Assembly;
    private static readonly Assembly BusinessAssembly = typeof(PaymentLedger.Business.DependencyInjection).Assembly;
    private static readonly Assembly SharedAssembly = typeof(ErrorCodes).Assembly;
    private static readonly Assembly ApiAssembly = typeof(Program).Assembly;

    [Fact]
    public void Repository_ShouldNotDependOnBusinessOrApi()
    {
        var result = Types.InAssembly(RepositoryAssembly)
            .ShouldNot()
            .HaveDependencyOnAny("PaymentLedger.Business", "PaymentLedger.Api")
            .GetResult();

        AssertArchitecture(result);
    }

    [Fact]
    public void Business_ShouldNotDependOnApi()
    {
        var result = Types.InAssembly(BusinessAssembly)
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
            .HaveDependencyOnAny("PaymentLedger.Api", "PaymentLedger.Business", "PaymentLedger.Repository")
            .GetResult();

        AssertArchitecture(result);
    }

    [Fact]
    public void Controllers_ShouldNotDependOnDbContext()
    {
        var result = Types.InAssembly(ApiAssembly)
            .That()
            .ResideInNamespace("PaymentLedger.Api.Controllers")
            .ShouldNot()
            .HaveDependencyOn("Microsoft.EntityFrameworkCore")
            .GetResult();

        AssertArchitecture(result);
    }

    [Fact]
    public void Business_ShouldNotDependOnSerilog()
    {
        var result = Types.InAssembly(BusinessAssembly)
            .ShouldNot()
            .HaveDependencyOn("Serilog")
            .GetResult();

        AssertArchitecture(result);
    }

    [Fact]
    public void Repository_ShouldNotDependOnSerilog()
    {
        var result = Types.InAssembly(RepositoryAssembly)
            .ShouldNot()
            .HaveDependencyOn("Serilog")
            .GetResult();

        AssertArchitecture(result);
    }

    [Fact]
    public void Middlewares_ShouldResideInApiMiddlewareNamespace()
    {
        var result = Types.InAssembly(ApiAssembly)
            .That()
            .HaveNameEndingWith("Middleware")
            .Should()
            .ResideInNamespace("PaymentLedger.Api.Middleware")
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
