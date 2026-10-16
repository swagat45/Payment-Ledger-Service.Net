using PaymentLedger.Repository.Exceptions;

namespace PaymentLedger.Repository.Entities;

public class OutboxMessage
{
    public Guid Id { get; private set; }

    public string Type { get; private set; } = string.Empty;

    public string Payload { get; private set; } = string.Empty;

    public DateTimeOffset OccurredOnUtc { get; private set; }

    public DateTimeOffset? ProcessedOnUtc { get; private set; }

    public string? Error { get; private set; }

    public int RetryCount { get; private set; }

    public OutboxMessage()
    {
    }

    public static OutboxMessage Create(string type, string payload, DateTimeOffset occurredOnUtc)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            throw new DomainException("Outbox message type is required.");
        }

        if (string.IsNullOrWhiteSpace(payload))
        {
            throw new DomainException("Outbox message payload is required.");
        }

        return new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Type = type.Trim(),
            Payload = payload,
            OccurredOnUtc = occurredOnUtc,
            ProcessedOnUtc = null,
            Error = null,
            RetryCount = 0
        };
    }

    public void MarkProcessed(DateTimeOffset processedOnUtc)
    {
        ProcessedOnUtc = processedOnUtc;
        Error = null;
    }

    public void RecordFailure(string error)
    {
        Error = error;
        RetryCount++;
    }
}
