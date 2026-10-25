using Microsoft.Extensions.Logging;

namespace PaymentLedger.Business.Events;

/// <summary>
/// Integration event publisher that streams outbox messages directly to the message broker.
/// Replaces the earlier simulation with real Apache Kafka / broker publishing.
/// </summary>
public sealed class LoggingIntegrationEventPublisher : IIntegrationEventPublisher
{
    private readonly KafkaIntegrationEventPublisher _kafkaPublisher;
    private readonly ILogger<LoggingIntegrationEventPublisher> _logger;

    public LoggingIntegrationEventPublisher(
        KafkaIntegrationEventPublisher kafkaPublisher,
        ILogger<LoggingIntegrationEventPublisher> logger)
    {
        _kafkaPublisher = kafkaPublisher;
        _logger = logger;
    }

    public async Task PublishAsync(
        Guid messageId,
        string eventType,
        string payload,
        DateTimeOffset occurredOnUtc,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Streaming outbox event '{EventType}' (MessageId: {MessageId}) to broker...",
            eventType,
            messageId);

        await _kafkaPublisher.PublishAsync(messageId, eventType, payload, occurredOnUtc, ct);
    }
}
