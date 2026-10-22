using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using PaymentLedger.Business.Common;
using PaymentLedger.Business.Events;
using PaymentLedger.Repository.Entities;
using PaymentLedger.Repository.Repositories;

namespace PaymentLedger.UnitTests.Business;

public class EventPublisherTests
{
    private readonly FakeOutboxRepository _outboxRepository = new();

    private EventPublisher CreatePublisher()
    {
        return new EventPublisher(_outboxRepository, NullLogger<EventPublisher>.Instance);
    }

    [Fact]
    public async Task PublishAsync_WhenCalledWithWalletCreatedEvent_SerializesAndAddsToRepository()
    {
        // Arrange
        var publisher = CreatePublisher();
        var walletId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var occurredOn = DateTimeOffset.UtcNow;
        var domainEvent = new WalletCreatedEvent(walletId, userId, "USD", occurredOn);

        // Act
        await publisher.PublishAsync(domainEvent);

        // Assert
        Assert.Single(_outboxRepository.Messages);
        var message = _outboxRepository.Messages[0];
        Assert.Equal(nameof(WalletCreatedEvent), message.Type);
        Assert.Equal(occurredOn, message.OccurredOnUtc);
        Assert.Null(message.ProcessedOnUtc);
        Assert.Equal(0, message.RetryCount);

        var json = JsonDocument.Parse(message.Payload);
        Assert.Equal(walletId.ToString(), json.RootElement.GetProperty("walletId").GetString());
        Assert.Equal(userId.ToString(), json.RootElement.GetProperty("userId").GetString());
        Assert.Equal("USD", json.RootElement.GetProperty("currency").GetString());
    }

    [Fact]
    public async Task PublishAsync_WhenCalledWithTransactionPostedEvent_SerializesAndAddsToRepository()
    {
        // Arrange
        var publisher = CreatePublisher();
        var transactionId = Guid.NewGuid();
        var sourceWalletId = Guid.NewGuid();
        var destinationWalletId = Guid.NewGuid();
        var occurredOn = DateTimeOffset.UtcNow;

        var domainEvent = new TransactionPostedEvent(
            TransactionId: transactionId,
            TransactionCode: "TX-12345",
            Type: "Debit",
            SourceWalletId: sourceWalletId,
            DestinationWalletId: destinationWalletId,
            Amount: 150.75m,
            Currency: "USD",
            Description: "Payment for order",
            OccurredOnUtc: occurredOn);

        // Act
        await publisher.PublishAsync(domainEvent);

        // Assert
        Assert.Single(_outboxRepository.Messages);
        var message = _outboxRepository.Messages[0];
        Assert.Equal(nameof(TransactionPostedEvent), message.Type);
        Assert.Equal(occurredOn, message.OccurredOnUtc);

        var json = JsonDocument.Parse(message.Payload);
        Assert.Equal(transactionId.ToString(), json.RootElement.GetProperty("transactionId").GetString());
        Assert.Equal("TX-12345", json.RootElement.GetProperty("transactionCode").GetString());
        Assert.Equal("Debit", json.RootElement.GetProperty("type").GetString());
        Assert.Equal(150.75m, json.RootElement.GetProperty("amount").GetDecimal());
    }

    [Fact]
    public async Task PublishAsync_WhenCalledWithNull_ThrowsArgumentNullException()
    {
        // Arrange
        var publisher = CreatePublisher();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => publisher.PublishAsync<WalletCreatedEvent>(null!));
    }

    private sealed class FakeOutboxRepository : IOutboxRepository
    {
        public List<OutboxMessage> Messages { get; } = [];

        public Task AddAsync(OutboxMessage message, CancellationToken cancellationToken = default)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<OutboxMessage>> GetUnprocessedBatchAsync(int batchSize, CancellationToken cancellationToken = default)
        {
            var batch = Messages
                .Where(m => m.ProcessedOnUtc == null)
                .OrderBy(m => m.OccurredOnUtc)
                .Take(batchSize)
                .ToList();

            return Task.FromResult<IReadOnlyList<OutboxMessage>>(batch);
        }

        public void Update(OutboxMessage message)
        {
        }
    }
}
