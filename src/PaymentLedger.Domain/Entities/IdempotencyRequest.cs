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
}
