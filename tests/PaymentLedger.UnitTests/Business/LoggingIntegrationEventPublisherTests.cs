using Confluent.Kafka;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using PaymentLedger.Business.Events;
using PaymentLedger.Shared.Options;

namespace PaymentLedger.UnitTests.Business;

public class LoggingIntegrationEventPublisherTests
{
    private readonly Mock<IProducer<string, string>> _producerMock = new();

    [Fact]
    public async Task PublishAsync_DelegatesDirectlyToRealKafkaPublisher()
    {
        // Arrange
        var options = Options.Create(new KafkaOptions
        {
            Enabled = true,
            Topic = "payment-ledger.events"
        });

        _producerMock
            .Setup(p => p.ProduceAsync(It.IsAny<string>(), It.IsAny<Message<string, string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeliveryResult<string, string>
            {
                Topic = "payment-ledger.events",
                Partition = new Partition(0),
                Offset = new Offset(1),
                Status = PersistenceStatus.Persisted
            });

        var kafkaPublisher = new KafkaIntegrationEventPublisher(
            options,
            NullLogger<KafkaIntegrationEventPublisher>.Instance,
            _producerMock.Object);

        var loggingPublisher = new LoggingIntegrationEventPublisher(
            kafkaPublisher,
            NullLogger<LoggingIntegrationEventPublisher>.Instance);

        var messageId = Guid.NewGuid();
        var occurredOn = DateTimeOffset.UtcNow;
        var payload = "{\"referenceId\":\"PO-2026-789\",\"amount\":500.00}";

        // Act
        await loggingPublisher.PublishAsync(messageId, "TransactionPostedEvent", payload, occurredOn);

        // Assert
        _producerMock.Verify(p => p.ProduceAsync(
            "payment-ledger.events",
            It.Is<Message<string, string>>(m => m.Key == "PO-2026-789" && m.Value == payload),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
