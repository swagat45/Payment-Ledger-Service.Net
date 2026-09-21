namespace PaymentLedger.Shared.Responses;

public interface IApiResponse
{
    bool Success { get; }
    string Message { get; }
    string? CorrelationId { get; set; }
}
