namespace PaymentLedger.Shared.Responses;

public sealed class ErrorResponse
{
    public bool Success { get; init; } = false;

    public string Message { get; init; } = string.Empty;

    public object? Data { get; init; }

    public IReadOnlyList<ErrorDetail> Errors { get; init; } = Array.Empty<ErrorDetail>();

    public string? CorrelationId { get; init; }

    public static ErrorResponse Create(
        string message,
        IEnumerable<ErrorDetail>? errors = null,
        string? correlationId = null)
    {
        return new ErrorResponse
        {
            Message = message,
            Data = null,
            Errors = errors?.ToArray() ?? Array.Empty<ErrorDetail>(),
            CorrelationId = correlationId
        };
    }
}

public sealed record ErrorDetail(string Code, string? Field, string Message);
