using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace PaymentLedger.Business.Events;

public sealed class LoggingIntegrationEventPublisher : IIntegrationEventPublisher
{
    private readonly ILogger<LoggingIntegrationEventPublisher> _logger;

    public LoggingIntegrationEventPublisher(ILogger<LoggingIntegrationEventPublisher> logger)
    {
        _logger = logger;
    }

    public Task PublishAsync(Guid messageId, string eventType, string payload, DateTimeOffset occurredOnUtc, CancellationToken ct = default)
    {
        // Deserialize payload to validate JSON structure and enable structured representation
        using var jsonDoc = JsonDocument.Parse(payload);
        var rawJson = jsonDoc.RootElement.GetRawText();

        _logger.LogInformation(
            "[Broker Simulated] Published message to Kafka topic 'payment-ledger.{EventType}' (RabbitMQ exchange 'payment-ledger.events', routingKey '{EventType}') | MessageId={MessageId} | OccurredOn={OccurredOnUtc} | Payload={Payload}",
            eventType,
            eventType,
            messageId,
            occurredOnUtc,
            rawJson);

        return Task.CompletedTask;
    }
}
