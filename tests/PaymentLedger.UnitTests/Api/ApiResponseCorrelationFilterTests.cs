using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using PaymentLedger.Api.Common;
using PaymentLedger.Api.Filters;
using PaymentLedger.Api.Middleware;
using PaymentLedger.Shared.Context;
using PaymentLedger.Shared.Responses;

namespace PaymentLedger.UnitTests.Api;

public class ApiResponseCorrelationFilterTests
{
    private const string ExpectedCorrelationId = "filter-correlation-id-999";

    [Fact]
    public async Task OnResultExecutionAsync_WhenApiResponseHasNoCorrelationId_InjectsCorrelationId()
    {
        // Arrange
        var provider = CreateCorrelationIdProvider(ExpectedCorrelationId);
        var filter = new ApiResponseCorrelationFilter(provider);

        var apiResponse = ApiResponse<string>.Ok("hello");
        var resultExecutingContext = CreateResultExecutingContext(apiResponse);

        var nextCalled = false;
        ResultExecutionDelegate next = () =>
        {
            nextCalled = true;
            return Task.FromResult(new ResultExecutedContext(resultExecutingContext, new List<IFilterMetadata>(), resultExecutingContext.Result, new object()));
        };

        // Act
        await filter.OnResultExecutionAsync(resultExecutingContext, next);

        // Assert
        Assert.True(nextCalled);
        Assert.Equal(ExpectedCorrelationId, apiResponse.CorrelationId);
    }

    [Fact]
    public async Task OnResultExecutionAsync_WhenApiResponseHasExistingCorrelationId_DoesNotOverwrite()
    {
        // Arrange
        const string existingId = "already-set-correlation-id";
        var provider = CreateCorrelationIdProvider(ExpectedCorrelationId);
        var filter = new ApiResponseCorrelationFilter(provider);

        var apiResponse = ApiResponse<string>.Ok("hello", correlationId: existingId);
        var resultExecutingContext = CreateResultExecutingContext(apiResponse);

        ResultExecutionDelegate next = () => Task.FromResult(new ResultExecutedContext(resultExecutingContext, new List<IFilterMetadata>(), resultExecutingContext.Result, new object()));

        // Act
        await filter.OnResultExecutionAsync(resultExecutingContext, next);

        // Assert
        Assert.Equal(existingId, apiResponse.CorrelationId);
    }

    [Fact]
    public async Task OnResultExecutionAsync_WhenErrorResponseHasNoCorrelationId_InjectsCorrelationId()
    {
        // Arrange
        var provider = CreateCorrelationIdProvider(ExpectedCorrelationId);
        var filter = new ApiResponseCorrelationFilter(provider);

        var errorResponse = ErrorResponse.Create("Some error");
        var resultExecutingContext = CreateResultExecutingContext(errorResponse);

        ResultExecutionDelegate next = () => Task.FromResult(new ResultExecutedContext(resultExecutingContext, new List<IFilterMetadata>(), resultExecutingContext.Result, new object()));

        // Act
        await filter.OnResultExecutionAsync(resultExecutingContext, next);

        // Assert
        Assert.Equal(ExpectedCorrelationId, errorResponse.CorrelationId);
    }

    private static ICorrelationIdProvider CreateCorrelationIdProvider(string correlationId)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Items[CorrelationIdMiddleware.CorrelationIdItemKey] = correlationId;
        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        return new CorrelationIdProvider(accessor);
    }

    private static ResultExecutingContext CreateResultExecutingContext(object responseValue)
    {
        var actionContext = new ActionContext(
            new DefaultHttpContext(),
            new RouteData(),
            new ActionDescriptor());

        return new ResultExecutingContext(
            actionContext,
            new List<IFilterMetadata>(),
            new ObjectResult(responseValue),
            new object());
    }
}
