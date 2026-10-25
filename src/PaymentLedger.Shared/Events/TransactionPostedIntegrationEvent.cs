namespace PaymentLedger.Shared.Events;

public record TransactionPostedIntegrationEvent
{
    public Guid TransactionId { get; init; }
    public string TransactionCode { get; init; } = string.Empty;
    public string? ReferenceId { get; init; }
    public string Type { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string Currency { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTimeOffset OccurredOnUtc { get; init; }

    public TransactionPostedIntegrationEvent()
    {
    }

    public TransactionPostedIntegrationEvent(
        Guid transactionId,
        string transactionCode,
        string? referenceId,
        string type,
        decimal amount,
        string currency,
        string status,
        DateTimeOffset occurredOnUtc)
    {
        TransactionId = transactionId;
        TransactionCode = transactionCode;
        ReferenceId = referenceId;
        Type = type;
        Amount = amount;
        Currency = currency;
        Status = status;
        OccurredOnUtc = occurredOnUtc;
    }
}
