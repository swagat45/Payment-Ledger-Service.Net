using Microsoft.AspNetCore.Http;
using PaymentLedger.Api.Common;
using PaymentLedger.Api.Middleware;

namespace PaymentLedger.UnitTests.Api;

public class CorrelationIdMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_WithExistingCorrelationId_PreservesHeaderAndItems()
    {
        // Arrange
        const string expectedCorrelationId = "incoming-correlation-id-123";
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.CorrelationIdHeaderName] = expectedCorrelationId;

        var nextCalled = false;
        RequestDelegate next = ctx =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new CorrelationIdMiddleware(next);

        // Act
        await middleware.InvokeAsync(context);
        await context.Response.StartAsync();

        // Assert
        Assert.True(nextCalled);
        Assert.Equal(expectedCorrelationId, context.Items[CorrelationIdMiddleware.CorrelationIdItemKey]);
        Assert.Equal(expectedCorrelationId, context.TraceIdentifier);
        Assert.True(context.Response.Headers.ContainsKey(CorrelationIdMiddleware.CorrelationIdHeaderName));
        Assert.Equal(expectedCorrelationId, context.Response.Headers[CorrelationIdMiddleware.CorrelationIdHeaderName].ToString());
    }

    [Fact]
    public async Task InvokeAsync_WithoutCorrelationId_GeneratesValidGuid()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var nextCalled = false;
        RequestDelegate next = ctx =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new CorrelationIdMiddleware(next);

        // Act
        await middleware.InvokeAsync(context);
        await context.Response.StartAsync();

        // Assert
        Assert.True(nextCalled);
        var correlationIdObj = context.Items[CorrelationIdMiddleware.CorrelationIdItemKey];
        Assert.NotNull(correlationIdObj);
        var correlationId = correlationIdObj!.ToString()!;

        Assert.True(Guid.TryParse(correlationId, out _));
        Assert.Equal(correlationId, context.TraceIdentifier);
        Assert.Equal(correlationId, context.Response.Headers[CorrelationIdMiddleware.CorrelationIdHeaderName].ToString());
    }

    [Fact]
    public void CorrelationIdProvider_ReturnsCorrelationIdFromHttpContextItems()
    {
        // Arrange
        const string expectedCorrelationId = "test-correlation-xyz";
        var context = new DefaultHttpContext();
        context.Items[CorrelationIdMiddleware.CorrelationIdItemKey] = expectedCorrelationId;

        var httpContextAccessor = new HttpContextAccessor { HttpContext = context };
        var provider = new CorrelationIdProvider(httpContextAccessor);

        // Act
        var result = provider.CorrelationId;

        // Assert
        Assert.Equal(expectedCorrelationId, result);
    }

    [Fact]
    public void CorrelationIdProvider_WhenHttpContextIsNull_ReturnsNonEmptyId()
    {
        // Arrange
        var httpContextAccessor = new HttpContextAccessor { HttpContext = null };
        var provider = new CorrelationIdProvider(httpContextAccessor);

        // Act
        var result = provider.CorrelationId;

        // Assert
        Assert.False(string.IsNullOrWhiteSpace(result));
    }

    [Fact]
    public async Task InvokeAsync_SetsAndCleansUpAmbientCorrelationContext()
    {
        // Arrange
        const string expectedCorrelationId = "corr-ambient-test";
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.CorrelationIdHeaderName] = expectedCorrelationId;

        string? capturedAmbient = null;
        RequestDelegate next = _ =>
        {
            capturedAmbient = PaymentLedger.Shared.Context.CorrelationContext.Current;
            return Task.CompletedTask;
        };

        var middleware = new CorrelationIdMiddleware(next);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal(expectedCorrelationId, capturedAmbient);
        Assert.Null(PaymentLedger.Shared.Context.CorrelationContext.Current);
    }

    [Fact]
    public async Task InvokeAsync_WhenNextThrows_CleansUpAmbientCorrelationContext()
    {
        // Arrange
        const string expectedCorrelationId = "corr-ambient-throw-test";
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.CorrelationIdHeaderName] = expectedCorrelationId;

        RequestDelegate next = _ => throw new InvalidOperationException("Simulated error in pipeline");
        var middleware = new CorrelationIdMiddleware(next);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => middleware.InvokeAsync(context));
        Assert.Null(PaymentLedger.Shared.Context.CorrelationContext.Current);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task InvokeAsync_WithEmptyOrWhitespaceHeader_GeneratesNewValidGuid(string headerValue)
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.CorrelationIdHeaderName] = headerValue;

        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var generatedId = context.Items[CorrelationIdMiddleware.CorrelationIdItemKey]?.ToString();
        Assert.NotNull(generatedId);
        Assert.True(Guid.TryParse(generatedId, out _));
        Assert.NotEqual(headerValue, generatedId);
    }

    [Fact]
    public async Task InvokeAsync_WithPaddedCorrelationId_TrimsWhitespace()
    {
        // Arrange
        const string rawId = "my-custom-corr-id";
        var context = new DefaultHttpContext();
        context.Request.Headers[CorrelationIdMiddleware.CorrelationIdHeaderName] = $"  {rawId}  \t";

        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal(rawId, context.Items[CorrelationIdMiddleware.CorrelationIdItemKey]);
        Assert.Equal(rawId, context.TraceIdentifier);
    }
}
