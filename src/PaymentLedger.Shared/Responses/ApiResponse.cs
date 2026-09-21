namespace PaymentLedger.Shared.Responses;

public class ApiResponse<T> : IApiResponse
{
    public bool Success { get; init; }

    public string Message { get; init; } = string.Empty;

    public T? Data { get; init; }

    public string? CorrelationId { get; set; }

    public static ApiResponse<T> Ok(T? data, string message = "Request completed successfully.", string? correlationId = null)
    {
        return new ApiResponse<T>
        {
            Success = true,
            Message = message,
            Data = data,
            CorrelationId = correlationId
        };
    }
}
