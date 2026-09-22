using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using PaymentLedger.Api.Middleware;
using PaymentLedger.Business.Exceptions;
using PaymentLedger.Repository.Exceptions;
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

        RequestDelegate next = _ => throw new PaymentLedger.Business.Exceptions.ValidationException("Amount", "Amount must be positive.");

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

    [Fact]
    public async Task InvokeAsync_WhenDuplicateRequestDomainExceptionThrown_Returns429TooManyRequests()
    {
        // Arrange
        const string correlationId = "corr-duplicate-test";
        var context = CreateHttpContextWithCorrelationId(correlationId);

        RequestDelegate next = _ => throw new DomainException("Duplicate request detected.", ErrorCodes.DuplicateRequest);

        var middleware = new GlobalExceptionHandlerMiddleware(
            next,
            NullLogger<GlobalExceptionHandlerMiddleware>.Instance,
            _mapper);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal(StatusCodes.Status429TooManyRequests, context.Response.StatusCode);
        Assert.Equal(correlationId, context.Response.Headers[CorrelationIdMiddleware.CorrelationIdHeaderName].ToString());

        var response = await ReadResponseBodyAsync(context);
        Assert.NotNull(response);
        Assert.False(response.Success);
        Assert.Equal("Duplicate request detected.", response.Message);
        Assert.Equal(correlationId, response.CorrelationId);
        Assert.Single(response.Errors);
        Assert.Equal(ErrorCodes.DuplicateRequest, response.Errors[0].Code);
    }

    [Fact]
    public async Task InvokeAsync_WhenConcurrencyConflictThrown_Returns409Conflict()
    {
        // Arrange
        const string correlationId = "corr-concurrency-test";
        var context = CreateHttpContextWithCorrelationId(correlationId);

        RequestDelegate next = _ => throw new DomainException("A concurrent operation is already in progress.", ErrorCodes.ConcurrencyConflict);

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
        Assert.Equal("A concurrent operation is already in progress.", response.Message);
        Assert.Equal(correlationId, response.CorrelationId);
        Assert.Single(response.Errors);
        Assert.Equal(ErrorCodes.ConcurrencyConflict, response.Errors[0].Code);
    }

    [Fact]
    public async Task InvokeAsync_WhenMultipleValidationFailuresThrown_Returns400WithDetails()
    {
        // Arrange
        const string correlationId = "corr-val-test";
        var context = CreateHttpContextWithCorrelationId(correlationId);

        var errors = new Dictionary<string, string[]>
        {
            ["Currency"] = ["Currency is required."],
            ["Amount"] = ["Amount must be greater than zero."]
        };
        RequestDelegate next = _ => throw new PaymentLedger.Business.Exceptions.ValidationException(errors);

        var middleware = new GlobalExceptionHandlerMiddleware(
            next,
            NullLogger<GlobalExceptionHandlerMiddleware>.Instance,
            _mapper);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        var response = await ReadResponseBodyAsync(context);
        Assert.NotNull(response);
        Assert.False(response.Success);
        Assert.Equal(2, response.Errors.Count);
        Assert.Contains(response.Errors, e => e.Field == "Currency" && e.Message == "Currency is required.");
        Assert.Contains(response.Errors, e => e.Field == "Amount" && e.Message == "Amount must be greater than zero.");
    }

    [Fact]
    public async Task InvokeAsync_WhenResponseHasAlreadyStarted_GracefullyReturnsWithoutThrowing()
    {
        // Arrange
        var context = CreateHttpContextWithCorrelationId("corr-started-test");
        await context.Response.StartAsync();

        RequestDelegate next = _ => throw new InvalidOperationException("Fails after response started");

        var middleware = new GlobalExceptionHandlerMiddleware(
            next,
            NullLogger<GlobalExceptionHandlerMiddleware>.Instance,
            _mapper);

        // Act & Assert (should not throw despite unhandled exception)
        var exception = await Record.ExceptionAsync(() => middleware.InvokeAsync(context));
        Assert.Null(exception);
    }

    [Fact]
    public async Task InvokeAsync_WhenAmbientCorrelationContextIsSet_UsesItForErrorEnvelope()
    {
        // Arrange
        const string ambientId = "ambient-corr-777";
        PaymentLedger.Shared.Context.CorrelationContext.Current = ambientId;
        try
        {
            var context = new DefaultHttpContext();
            context.Response.Body = new MemoryStream();

            RequestDelegate next = _ => throw new DomainException("Wallet not found.", ErrorCodes.WalletNotFound);

            var middleware = new GlobalExceptionHandlerMiddleware(
                next,
                NullLogger<GlobalExceptionHandlerMiddleware>.Instance,
                _mapper);

            // Act
            await middleware.InvokeAsync(context);

            // Assert
            var response = await ReadResponseBodyAsync(context);
            Assert.NotNull(response);
            Assert.Equal(ambientId, response.CorrelationId);
            Assert.Equal(ambientId, context.Response.Headers[CorrelationIdMiddleware.CorrelationIdHeaderName].ToString());
        }
        finally
        {
            PaymentLedger.Shared.Context.CorrelationContext.Current = null;
        }
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
