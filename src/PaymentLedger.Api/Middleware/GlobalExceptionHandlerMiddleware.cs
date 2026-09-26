using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using PaymentLedger.Business.Exceptions;
using PaymentLedger.Shared.Context;

namespace PaymentLedger.Api.Middleware;

public sealed class GlobalExceptionHandlerMiddleware
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlerMiddleware> _logger;
    private readonly IExceptionToResponseMapper _exceptionMapper;
    private readonly ICorrelationIdProvider? _correlationIdProvider;

    [ActivatorUtilitiesConstructor]
    public GlobalExceptionHandlerMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionHandlerMiddleware> logger,
        IExceptionToResponseMapper exceptionMapper)
        : this(next, logger, exceptionMapper, null)
    {
    }

    public GlobalExceptionHandlerMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionHandlerMiddleware> logger,
        IExceptionToResponseMapper exceptionMapper,
        ICorrelationIdProvider? correlationIdProvider)
    {
        _next = next;
        _logger = logger;
        _exceptionMapper = exceptionMapper;
        _correlationIdProvider = correlationIdProvider;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var correlationId = GetCorrelationId(context);
        var (statusCode, errorResponse) = _exceptionMapper.Map(exception, correlationId);

        if (statusCode >= 500)
        {
            _logger.LogError(
                exception,
                "Unhandled exception occurred while processing request. Status: {StatusCode}, CorrelationId: {CorrelationId}, Message: {Message}",
                statusCode,
                correlationId,
                exception.Message);
        }
        else
        {
            _logger.LogWarning(
                "Client error occurred. Status: {StatusCode}, CorrelationId: {CorrelationId}, Message: {Message}",
                statusCode,
                correlationId,
                exception.Message);
        }

        if (context.Response.HasStarted)
        {
            _logger.LogWarning("The response has already started, cannot write exception response envelope.");
            return;
        }

        if (context.Response.Body.CanSeek)
        {
            context.Response.Body.SetLength(0);
        }

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        if (!context.Response.Headers.ContainsKey(CorrelationIdMiddleware.CorrelationIdHeaderName) && !string.IsNullOrWhiteSpace(correlationId))
        {
            context.Response.Headers.Append(CorrelationIdMiddleware.CorrelationIdHeaderName, correlationId);
        }

        var json = JsonSerializer.Serialize(errorResponse, SerializerOptions);
        await context.Response.WriteAsync(json);
    }

    private string GetCorrelationId(HttpContext context)
    {
        if (!string.IsNullOrWhiteSpace(CorrelationContext.Current))
        {
            return CorrelationContext.Current;
        }

        if (!string.IsNullOrWhiteSpace(_correlationIdProvider?.CorrelationId))
        {
            return _correlationIdProvider.CorrelationId;
        }

        var provider = context.RequestServices?.GetService<ICorrelationIdProvider>();
        if (!string.IsNullOrWhiteSpace(provider?.CorrelationId))
        {
            return provider.CorrelationId;
        }

        if (context.Items.TryGetValue(CorrelationIdMiddleware.CorrelationIdItemKey, out var item) &&
            item is string itemCorrelationId &&
            !string.IsNullOrWhiteSpace(itemCorrelationId))
        {
            return itemCorrelationId;
        }

        return context.TraceIdentifier ?? Guid.NewGuid().ToString();
    }
}
