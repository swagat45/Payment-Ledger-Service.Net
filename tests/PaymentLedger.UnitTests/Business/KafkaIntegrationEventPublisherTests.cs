using System.Text;
using Confluent.Kafka;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using PaymentLedger.Business.Events;
using PaymentLedger.Shared.Options;

namespace PaymentLedger.UnitTests.Business;

public class KafkaIntegrationEventPublisherTests
{
    private readonly Mock<IProducer<string, string>> _producerMock = new();
    private readonly NullLogger<KafkaIntegrationEventPublisher> _logger = NullLogger<KafkaIntegrationEventPublisher>.Instance;

    [Fact]
    public void ResolveMessageKey_WhenReferenceIdPresent_ReturnsReferenceId()
    {
        // Arrange
        var messageId = Guid.NewGuid();
        var payload = "{\"referenceId\":\"PO-2026-001\",\"transactionId\":\"TXN-123\"}";

        // Act
        var key = KafkaIntegrationEventPublisher.ResolveMessageKey(messageId, payload);

        // Assert
        Assert.Equal("PO-2026-001", key);
    }

    [Fact]
    public void ResolveMessageKey_WhenOnlyTransactionIdPresent_ReturnsTransactionId()
    {
        // Arrange
        var messageId = Guid.NewGuid();
        var payload = "{\"transactionId\":\"TXN-456\"}";

        // Act
        var key = KafkaIntegrationEventPublisher.ResolveMessageKey(messageId, payload);

        // Assert
        Assert.Equal("TXN-456", key);
    }

    [Fact]
    public void ResolveMessageKey_WhenNoIdentifiableFields_ReturnsMessageId()
    {
        // Arrange
        var messageId = Guid.NewGuid();
        var payload = "{\"otherField\":\"some-value\"}";

        // Act
        var key = KafkaIntegrationEventPublisher.ResolveMessageKey(messageId, payload);

        // Assert
        Assert.Equal(messageId.ToString(), key);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-valid-json")]
    public void ResolveMessageKey_WhenInvalidOrEmptyPayload_ReturnsMessageId(string? invalidPayload)
    {
        // Arrange
        var messageId = Guid.NewGuid();

        // Act
        var key = KafkaIntegrationEventPublisher.ResolveMessageKey(messageId, invalidPayload!);

        // Assert
        Assert.Equal(messageId.ToString(), key);
    }

    [Fact]
    public async Task PublishAsync_WhenKafkaDisabled_DoesNotProduceMessage()
    {
        // Arrange
        var options = Options.Create(new KafkaOptions { Enabled = false });
        using var publisher = new KafkaIntegrationEventPublisher(options, _logger, _producerMock.Object);

        // Act
        await publisher.PublishAsync(Guid.NewGuid(), "TransactionPostedEvent", "{}", DateTimeOffset.UtcNow);

        // Assert
        _producerMock.Verify(p => p.ProduceAsync(
            It.IsAny<string>(),
            It.IsAny<Message<string, string>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PublishAsync_WhenKafkaEnabled_ProducesMessageWithHeadersAndKey()
    {
        // Arrange
        var options = Options.Create(new KafkaOptions
        {
            Enabled = true,
            Topic = "payment-ledger.events",
            BootstrapServers = "localhost:9092"
        });

        var messageId = Guid.NewGuid();
        var occurredOn = DateTimeOffset.UtcNow;
        var payload = "{\"referenceId\":\"PV-2026-999\",\"amount\":150.00}";

        Message<string, string>? capturedMessage = null;
        string? capturedTopic = null;

        _producerMock
            .Setup(p => p.ProduceAsync(It.IsAny<string>(), It.IsAny<Message<string, string>>(), It.IsAny<CancellationToken>()))
            .Callback<string, Message<string, string>, CancellationToken>((topic, msg, _) =>
            {
                capturedTopic = topic;
                capturedMessage = msg;
            })
            .ReturnsAsync(new DeliveryResult<string, string>
            {
                Topic = "payment-ledger.events",
                Partition = new Partition(1),
                Offset = new Offset(42),
                Status = PersistenceStatus.Persisted
            });

        using var publisher = new KafkaIntegrationEventPublisher(options, _logger, _producerMock.Object);

        // Act
        await publisher.PublishAsync(messageId, "TransactionPostedEvent", payload, occurredOn);

        // Assert
        Assert.Equal("payment-ledger.events", capturedTopic);
        Assert.NotNull(capturedMessage);
        Assert.Equal("PV-2026-999", capturedMessage.Key);
        Assert.Equal(payload, capturedMessage.Value);

        Assert.NotNull(capturedMessage.Headers);
        var headerDict = capturedMessage.Headers.ToDictionary(h => h.Key, h => Encoding.UTF8.GetString(h.GetValueBytes()));
        Assert.Equal(messageId.ToString(), headerDict["message-id"]);
        Assert.Equal("TransactionPostedEvent", headerDict["event-type"]);
        Assert.Equal(occurredOn.ToString("O"), headerDict["occurred-on"]);
    }

    [Fact]
    public async Task PublishAsync_WhenProduceThrowsProduceException_RethrowsException()
    {
        // Arrange
        var options = Options.Create(new KafkaOptions { Enabled = true, Topic = "test-topic" });
        var messageId = Guid.NewGuid();

        _producerMock
            .Setup(p => p.ProduceAsync(It.IsAny<string>(), It.IsAny<Message<string, string>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ProduceException<string, string>(
                new Error(ErrorCode.BrokerNotAvailable, "Broker unavailable"),
                new DeliveryResult<string, string>()));

        using var publisher = new KafkaIntegrationEventPublisher(options, _logger, _producerMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ProduceException<string, string>>(() =>
            publisher.PublishAsync(messageId, "TransactionPostedEvent", "{}", DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Dispose_WhenProducerCreated_FlushesAndDisposesProducer()
    {
        // Arrange
        var options = Options.Create(new KafkaOptions { Enabled = true });
        var publisher = new KafkaIntegrationEventPublisher(options, _logger, _producerMock.Object);

        // Trigger producer lazy initialization
        _ = publisher.PublishAsync(Guid.NewGuid(), "TestEvent", "{}", DateTimeOffset.UtcNow);

        // Act
        publisher.Dispose();

        // Assert
        _producerMock.Verify(p => p.Flush(It.IsAny<TimeSpan>()), Times.Once);
        _producerMock.Verify(p => p.Dispose(), Times.Once);
    }
}
