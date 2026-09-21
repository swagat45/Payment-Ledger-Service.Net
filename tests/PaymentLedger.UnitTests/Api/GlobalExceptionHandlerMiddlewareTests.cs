using System.Text.Json;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using PaymentLedger.Api.Middleware;
using PaymentLedger.Application.Common.Exceptions;
using PaymentLedger.Domain.Exceptions;
using PaymentLedger.Shared.Constants;
using PaymentLedger.Shared.Responses;

namespace PaymentLedger.UnitTests.Api;

public class GlobalExceptionHandlerMiddlewareTests
{
    private readonly ExceptionToResponseMapper _mapper = new();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [Fact]
    public async Task InvokeAsync_WhenValidationExceptionThrown_Returns400WithErrorsAndCorrelationId()
    {
        // Arrange
        const string correlationId = "corr-validation-test";
        var context = CreateHttpContextWithCorrelationId(correlationId);

        var failures = new List<ValidationFailure>
        {
            new("Amount", "Amount must be positive.")
        };
        RequestDelegate next = _ => throw new PaymentLedger.Application.Exceptions.ValidationException(failures);

        var middleware = new GlobalExceptionHandlerMiddleware(
            next,
            NullLogger<GlobalExceptionHandlerMiddleware>.Instance,
            _mapper);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.Equal("application/json", context.Response.ContentType);
        Assert.Equal(correlationId, context.Response.Headers[CorrelationIdMiddleware.CorrelationIdHeaderName].ToString());

        var response = await ReadResponseBodyAsync(context);
        Assert.NotNull(response);
        Assert.False(response.Success);
        Assert.Equal(correlationId, response.CorrelationId);
        Assert.Single(response.Errors);
        Assert.Equal("Amount", response.Errors[0].Field);
        Assert.Equal("Amount must be positive.", response.Errors[0].Message);
    }

    [Fact]
    public async Task InvokeAsync_WhenDomainNotFoundExceptionThrown_Returns404WithCorrelationId()
    {
        // Arrange
        const string correlationId = "corr-notfound-test";
        var context = CreateHttpContextWithCorrelationId(correlationId);

        RequestDelegate next = _ => throw new DomainException("Wallet not found.", ErrorCodes.WalletNotFound);

        var middleware = new GlobalExceptionHandlerMiddleware(
            next,
            NullLogger<GlobalExceptionHandlerMiddleware>.Instance,
            _mapper);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
        Assert.Equal(correlationId, context.Response.Headers[CorrelationIdMiddleware.CorrelationIdHeaderName].ToString());

        var response = await ReadResponseBodyAsync(context);
        Assert.NotNull(response);
        Assert.False(response.Success);
        Assert.Equal("Wallet not found.", response.Message);
        Assert.Equal(correlationId, response.CorrelationId);
        Assert.Single(response.Errors);
        Assert.Equal(ErrorCodes.WalletNotFound, response.Errors[0].Code);
    }

    [Fact]
    public async Task InvokeAsync_WhenDomainConflictExceptionThrown_Returns409WithCorrelationId()
    {
        // Arrange
        const string correlationId = "corr-conflict-test";
        var context = CreateHttpContextWithCorrelationId(correlationId);

        RequestDelegate next = _ => throw new DomainException("Insufficient balance.", ErrorCodes.InsufficientBalance);

        var middleware = new GlobalExceptionHandlerMiddleware(
            next,
            NullLogger<GlobalExceptionHandlerMiddleware>.Instance,
            _mapper);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal(StatusCodes.Status409Conflict, context.Response.StatusCode);
        Assert.Equal(correlationId, context.Response.Headers[CorrelationIdMiddleware.CorrelationIdHeaderName].ToString());

        var response = await ReadResponseBodyAsync(context);
        Assert.NotNull(response);
        Assert.False(response.Success);
        Assert.Equal("Insufficient balance.", response.Message);
        Assert.Equal(correlationId, response.CorrelationId);
        Assert.Single(response.Errors);
        Assert.Equal(ErrorCodes.InsufficientBalance, response.Errors[0].Code);
    }

    [Fact]
    public async Task InvokeAsync_WhenUnhandledExceptionThrown_Returns500WithoutLeakingDetails()
    {
        // Arrange
        const string correlationId = "corr-internal-test";
        var context = CreateHttpContextWithCorrelationId(correlationId);

        RequestDelegate next = _ => throw new InvalidOperationException("Fatal database connection timeout password=secret");

        var middleware = new GlobalExceptionHandlerMiddleware(
            next,
            NullLogger<GlobalExceptionHandlerMiddleware>.Instance,
            _mapper);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        Assert.Equal(correlationId, context.Response.Headers[CorrelationIdMiddleware.CorrelationIdHeaderName].ToString());

        var response = await ReadResponseBodyAsync(context);
        Assert.NotNull(response);
        Assert.False(response.Success);
        Assert.Equal(correlationId, response.CorrelationId);
        Assert.DoesNotContain("secret", response.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("timeout", response.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("An unexpected error occurred. Please try again later.", response.Message);
        Assert.Single(response.Errors);
        Assert.Equal(ErrorCodes.InternalServerError, response.Errors[0].Code);
    }

    [Fact]
    public async Task InvokeAsync_WhenNoExceptionThrown_CallsNextSuccessfully()
    {
        // Arrange
        var context = CreateHttpContextWithCorrelationId("corr-success-test");
        var nextCalled = false;

        RequestDelegate next = ctx =>
        {
            nextCalled = true;
            ctx.Response.StatusCode = StatusCodes.Status200OK;
            return Task.CompletedTask;
        };

        var middleware = new GlobalExceptionHandlerMiddleware(
            next,
            NullLogger<GlobalExceptionHandlerMiddleware>.Instance,
            _mapper);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.True(nextCalled);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    private static DefaultHttpContext CreateHttpContextWithCorrelationId(string correlationId)
    {
        var context = new DefaultHttpContext();
        context.Items[CorrelationIdMiddleware.CorrelationIdItemKey] = correlationId;
        context.TraceIdentifier = correlationId;
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static async Task<ErrorResponse?> ReadResponseBodyAsync(HttpContext context)
    {
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        var json = await reader.ReadToEndAsync();
        return JsonSerializer.Deserialize<ErrorResponse>(json, JsonOptions);
    }
}
