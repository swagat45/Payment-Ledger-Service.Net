using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PaymentLedger.Shared.Options;

namespace PaymentLedger.Business.Events;

public sealed class KafkaIntegrationEventPublisher : IIntegrationEventPublisher, IDisposable
{
    private readonly KafkaOptions _options;
    private readonly ILogger<KafkaIntegrationEventPublisher> _logger;
    private readonly Lazy<IProducer<string, string>> _producerLazy;

    public KafkaIntegrationEventPublisher(
        IOptions<KafkaOptions> options,
        ILogger<KafkaIntegrationEventPublisher> logger,
        IProducer<string, string>? producer = null)
    {
        _options = options?.Value ?? new KafkaOptions();
        _logger = logger;

        if (producer != null)
        {
            _producerLazy = new Lazy<IProducer<string, string>>(() => producer);
        }
        else
        {
            _producerLazy = new Lazy<IProducer<string, string>>(CreateProducer);
        }
    }

    private IProducer<string, string> CreateProducer()
    {
        var config = new ProducerConfig
        {
            BootstrapServers = _options.BootstrapServers,
            Acks = Acks.All,
            EnableIdempotence = true,
            MessageSendMaxRetries = 3,
            RetryBackoffMs = 100,
            SocketTimeoutMs = 10000
        };

        return new ProducerBuilder<string, string>(config).Build();
    }

    public async Task PublishAsync(
        Guid messageId,
        string eventType,
        string payload,
        DateTimeOffset occurredOnUtc,
        CancellationToken ct = default)
    {
        if (!_options.Enabled)
        {
            _logger.LogDebug(
                "Kafka publishing is disabled. Skipping event '{EventType}' for MessageId: {MessageId}.",
                eventType,
                messageId);
            return;
        }

        var topic = _options.Topic;
        var key = ResolveMessageKey(messageId, payload);

        var headers = new Headers
        {
            { "message-id", Encoding.UTF8.GetBytes(messageId.ToString()) },
            { "event-type", Encoding.UTF8.GetBytes(eventType) },
            { "occurred-on", Encoding.UTF8.GetBytes(occurredOnUtc.ToString("O")) }
        };

        var message = new Message<string, string>
        {
            Key = key,
            Value = payload,
            Headers = headers
        };

        try
        {
            var deliveryResult = await _producerLazy.Value.ProduceAsync(topic, message, ct);

            _logger.LogInformation(
                "Published integration event '{EventType}' to Kafka topic '{Topic}' [Partition: {Partition}, Offset: {Offset}] | MessageId: {MessageId} | Key: {Key}",
                eventType,
                deliveryResult.Topic,
                deliveryResult.Partition.Value,
                deliveryResult.Offset.Value,
                messageId,
                key);
        }
        catch (ProduceException<string, string> ex)
        {
            _logger.LogError(
                ex,
                "Delivery failed for integration event '{EventType}' to Kafka topic '{Topic}' | Reason: {Reason} | MessageId: {MessageId}",
                eventType,
                topic,
                ex.Error.Reason,
                messageId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unexpected failure publishing integration event '{EventType}' to Kafka topic '{Topic}' | MessageId: {MessageId}",
                eventType,
                topic,
                messageId);
            throw;
        }
    }

    public static string ResolveMessageKey(Guid messageId, string payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return messageId.ToString();
        }

        try
        {
            using var doc = JsonDocument.Parse(payload);
            if (doc.RootElement.TryGetProperty("referenceId", out var refElem) &&
                refElem.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(refElem.GetString()))
            {
                return refElem.GetString()!;
            }

            if (doc.RootElement.TryGetProperty("transactionId", out var txnElem) &&
                txnElem.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(txnElem.GetString()))
            {
                return txnElem.GetString()!;
            }
        }
        catch
        {
            // If JSON parsing fails, fall back to messageId
        }

        return messageId.ToString();
    }

    public void Dispose()
    {
        if (_producerLazy.IsValueCreated)
        {
            try
            {
                _producerLazy.Value.Flush(TimeSpan.FromSeconds(5));
                _producerLazy.Value.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error disposing Kafka producer.");
            }
        }
    }
}
