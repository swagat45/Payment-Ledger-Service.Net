using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PaymentLedger.Business.Events;
using PaymentLedger.Business.Services;
using PaymentLedger.Repository.Abstractions;
using PaymentLedger.Repository.Entities;
using PaymentLedger.Repository.Repositories;
using PaymentLedger.Shared.Options;
using StackExchange.Redis;

namespace PaymentLedger.UnitTests.Business;

public class OutboxDispatcherServiceTests
{
    private readonly FakeOutboxRepository _outboxRepository = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeIntegrationEventPublisher _eventPublisher = new();
    private readonly FakeDateTimeProvider _dateTimeProvider = new(DateTimeOffset.UtcNow);

    private OutboxDispatcherService CreateService(OutboxOptions? options = null)
    {
        var optionsWrapper = options != null ? Options.Create(options) : null;

        return new OutboxDispatcherService(
            _outboxRepository,
            _unitOfWork,
            _eventPublisher,
            Array.Empty<IConnectionMultiplexer>(),
            _dateTimeProvider,
            NullLogger<OutboxDispatcherService>.Instance,
            optionsWrapper);
    }

    [Fact]
    public async Task ProcessBatchAsync_WhenUnprocessedMessagesExist_PublishesEventsMarksAsProcessedAndCommitsTransaction()
    {
        // Arrange
        var service = CreateService();
        var msg1 = OutboxMessage.Create("WalletCreatedEvent", "{\"walletId\":\"111\"}", _dateTimeProvider.UtcNow);
        var msg2 = OutboxMessage.Create("TransactionPostedEvent", "{\"txnId\":\"222\"}", _dateTimeProvider.UtcNow);

        await _outboxRepository.AddAsync(msg1);
        await _outboxRepository.AddAsync(msg2);

        // Act
        var processedCount = await service.ProcessBatchAsync();

        // Assert
        Assert.Equal(2, processedCount);
        Assert.Equal(2, _eventPublisher.DispatchedEvents.Count);
        Assert.NotNull(msg1.ProcessedOnUtc);
        Assert.NotNull(msg2.ProcessedOnUtc);
        Assert.Null(msg1.Error);
        Assert.Null(msg2.Error);
        Assert.True(_unitOfWork.TransactionBegun);
        Assert.True(_unitOfWork.ChangesSaved);
        Assert.True(_unitOfWork.TransactionCommitted);
    }

    [Fact]
    public async Task ProcessBatchAsync_WhenPublishingFails_RecordsFailureAndCommitsBatch()
    {
        // Arrange
        _eventPublisher.ThrowOnPublish = true;
        _eventPublisher.ExceptionToThrow = new InvalidOperationException("Kafka cluster unreachable");

        var service = CreateService();
        var msg = OutboxMessage.Create("WalletCreatedEvent", "{\"walletId\":\"111\"}", _dateTimeProvider.UtcNow);
        await _outboxRepository.AddAsync(msg);

        // Act
        var processedCount = await service.ProcessBatchAsync();

        // Assert
        Assert.Equal(0, processedCount);
        Assert.Null(msg.ProcessedOnUtc);
        Assert.Equal(1, msg.RetryCount);
        Assert.Equal("Kafka cluster unreachable", msg.Error);
        Assert.True(_unitOfWork.TransactionBegun);
        Assert.True(_unitOfWork.ChangesSaved);
        Assert.True(_unitOfWork.TransactionCommitted);
    }

    [Fact]
    public async Task ProcessBatchAsync_WhenMessageExceedsMaxRetries_SkipsMessage()
    {
        // Arrange
        var options = new OutboxOptions { MaxRetries = 3 };
        var service = CreateService(options: options);

        var msg = OutboxMessage.Create("WalletCreatedEvent", "{\"walletId\":\"111\"}", _dateTimeProvider.UtcNow);
        msg.RecordFailure("Error 1");
        msg.RecordFailure("Error 2");
        msg.RecordFailure("Error 3"); // RetryCount == 3 >= MaxRetries

        await _outboxRepository.AddAsync(msg);

        // Act
        var processedCount = await service.ProcessBatchAsync();

        // Assert
        Assert.Equal(0, processedCount);
        Assert.Empty(_eventPublisher.DispatchedEvents);
        Assert.Null(msg.ProcessedOnUtc);
        Assert.True(_unitOfWork.TransactionBegun);
        Assert.True(_unitOfWork.TransactionCommitted);
    }

    [Fact]
    public async Task ProcessBatchAsync_WhenNoUnprocessedMessages_RollsBackTransactionWithoutSaving()
    {
        // Arrange
        var service = CreateService();

        // Act
        var processedCount = await service.ProcessBatchAsync();

        // Assert
        Assert.Equal(0, processedCount);
        Assert.True(_unitOfWork.TransactionBegun);
        Assert.True(_unitOfWork.TransactionRolledBack);
        Assert.False(_unitOfWork.TransactionCommitted);
        Assert.False(_unitOfWork.ChangesSaved);
        Assert.Empty(_eventPublisher.DispatchedEvents);
    }

    [Fact]
    public async Task ProcessBatchAsync_WhenOutboxDisabled_ReturnsZeroImmediatelyWithoutTransaction()
    {
        // Arrange
        var options = new OutboxOptions { Enabled = false };
        var service = CreateService(options: options);

        var msg = OutboxMessage.Create("WalletCreatedEvent", "{\"walletId\":\"111\"}", _dateTimeProvider.UtcNow);
        await _outboxRepository.AddAsync(msg);

        // Act
        var processedCount = await service.ProcessBatchAsync();

        // Assert
        Assert.Equal(0, processedCount);
        Assert.Empty(_eventPublisher.DispatchedEvents);
        Assert.Null(msg.ProcessedOnUtc);
        Assert.False(_unitOfWork.TransactionBegun);
        Assert.False(_unitOfWork.ChangesSaved);
    }

    [Fact]
    public async Task ProcessBatchAsync_WhenUnexpectedExceptionOccurs_RollsBackTransactionAndThrows()
    {
        // Arrange
        _unitOfWork.ThrowOnSaveChanges = true;
        var service = CreateService();
        var msg = OutboxMessage.Create("WalletCreatedEvent", "{\"walletId\":\"111\"}", _dateTimeProvider.UtcNow);
        await _outboxRepository.AddAsync(msg);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ProcessBatchAsync());

        Assert.True(_unitOfWork.TransactionBegun);
        Assert.True(_unitOfWork.TransactionRolledBack);
        Assert.False(_unitOfWork.TransactionCommitted);
    }

    #region Fakes

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

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public bool ChangesSaved { get; private set; }
        public bool TransactionBegun { get; private set; }
        public bool TransactionCommitted { get; private set; }
        public bool TransactionRolledBack { get; private set; }
        public bool ThrowOnSaveChanges { get; set; }

        public Task<int> SaveChangesAsync(CancellationToken ct = default)
        {
            if (ThrowOnSaveChanges)
            {
                throw new InvalidOperationException("Database connection terminated during save.");
            }

            ChangesSaved = true;
            return Task.FromResult(1);
        }

        public Task BeginTransactionAsync(CancellationToken ct = default)
        {
            TransactionBegun = true;
            return Task.CompletedTask;
        }

        public Task CommitTransactionAsync(CancellationToken ct = default)
        {
            TransactionCommitted = true;
            return Task.CompletedTask;
        }

        public Task RollbackTransactionAsync(CancellationToken ct = default)
        {
            TransactionRolledBack = true;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeIntegrationEventPublisher : IIntegrationEventPublisher
    {
        public List<(Guid Id, string Type, string Payload)> DispatchedEvents { get; } = [];
        public bool ThrowOnPublish { get; set; }
        public Exception? ExceptionToThrow { get; set; }

        public Task PublishAsync(Guid messageId, string eventType, string payload, DateTimeOffset occurredOnUtc, CancellationToken ct = default)
        {
            if (ThrowOnPublish)
            {
                throw ExceptionToThrow ?? new Exception("Publish failed");
            }

            DispatchedEvents.Add((messageId, eventType, payload));
            return Task.CompletedTask;
        }
    }

    private sealed class FakeDateTimeProvider : IDateTimeProvider
    {
        public DateTimeOffset UtcNow { get; }
        public FakeDateTimeProvider(DateTimeOffset utcNow) => UtcNow = utcNow;
    }

    #endregion
}
