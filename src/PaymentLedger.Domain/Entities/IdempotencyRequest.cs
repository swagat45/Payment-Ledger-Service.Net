namespace PaymentLedger.Domain.Entities;

public class IdempotencyRequest
{
    public Guid Id { get; private set; }

    public string IdempotencyKey { get; private set; } = string.Empty;

    public string Endpoint { get; private set; } = string.Empty;

    public string RequestHash { get; private set; } = string.Empty;

    public string? ResponseBody { get; private set; }

    public int? StatusCode { get; private set; }

    public string Status { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public IdempotencyRequest()
    {
    }

    public static IdempotencyRequest Create(
        string idempotencyKey,
        string endpoint,
        string requestHash,
        string? responseBody,
        int? statusCode,
        string status,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            throw new PaymentLedger.Domain.Exceptions.DomainException("Idempotency key is required.");
        }

        if (string.IsNullOrWhiteSpace(endpoint))
        {
            throw new PaymentLedger.Domain.Exceptions.DomainException("Endpoint is required.");
        }

        if (string.IsNullOrWhiteSpace(requestHash))
        {
            throw new PaymentLedger.Domain.Exceptions.DomainException("Request hash is required.");
        }

        return new IdempotencyRequest
        {
            Id = Guid.NewGuid(),
            IdempotencyKey = idempotencyKey.Trim(),
            Endpoint = endpoint.Trim(),
            RequestHash = requestHash.Trim(),
            ResponseBody = responseBody,
            StatusCode = statusCode,
            Status = status,
            CreatedAt = createdAt,
            ExpiresAt = expiresAt
        };
    }
}
