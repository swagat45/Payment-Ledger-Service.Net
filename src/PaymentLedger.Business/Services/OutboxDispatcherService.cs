using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PaymentLedger.Business.Events;
using PaymentLedger.Repository.Abstractions;
using PaymentLedger.Repository.Repositories;
using PaymentLedger.Shared.Options;
using StackExchange.Redis;

namespace PaymentLedger.Business.Services;

public sealed class OutboxDispatcherService : IOutboxDispatcher
{
    private readonly IOutboxRepository _outboxRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IIntegrationEventPublisher _eventPublisher;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<OutboxDispatcherService> _logger;
    private readonly OutboxOptions _options;

    public OutboxDispatcherService(
        IOutboxRepository outboxRepository,
        IUnitOfWork unitOfWork,
        IIntegrationEventPublisher eventPublisher,
        IEnumerable<IConnectionMultiplexer> redisProviders,
        IDateTimeProvider dateTimeProvider,
        ILogger<OutboxDispatcherService> logger,
        IOptions<OutboxOptions>? options = null)
    {
        _outboxRepository = outboxRepository;
        _unitOfWork = unitOfWork;
        _eventPublisher = eventPublisher;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
        _options = options?.Value ?? new OutboxOptions();
    }

    public async Task<int> ProcessBatchAsync(CancellationToken ct = default)
    {
        if (!_options.Enabled)
        {
            _logger.LogDebug("Outbox dispatcher is disabled. Skipping execution.");
            return 0;
        }

        var batchSize = Math.Max(1, _options.BatchSize);
        var maxRetries = Math.Max(1, _options.MaxRetries);

        // Open an explicit transaction so PostgreSQL 'SELECT ... FOR UPDATE SKIP LOCKED'
        // row locks are held throughout processing, preventing concurrent worker overlap.
        await _unitOfWork.BeginTransactionAsync(ct);

        try
        {
            var messages = await _outboxRepository.GetUnprocessedBatchAsync(batchSize, ct);
            if (messages.Count == 0)
            {
                await _unitOfWork.RollbackTransactionAsync(ct);
                return 0;
            }

            var processedCount = 0;
            var utcNow = _dateTimeProvider.UtcNow;

            foreach (var message in messages)
            {
                if (message.RetryCount >= maxRetries)
                {
                    _logger.LogWarning(
                        "Outbox message {MessageId} of type '{Type}' exceeded max retries ({MaxRetries}). Skipping.",
                        message.Id,
                        message.Type,
                        maxRetries);
                    continue;
                }

                try
                {
                    await _eventPublisher.PublishAsync(
                        message.Id,
                        message.Type,
                        message.Payload,
                        message.OccurredOnUtc,
                        ct);

                    message.MarkProcessed(utcNow);
                    processedCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Failed to dispatch outbox message {MessageId} of type '{Type}'. Attempt {Attempt}/{MaxRetries}.",
                        message.Id,
                        message.Type,
                        message.RetryCount + 1,
                        maxRetries);

                    message.RecordFailure(ex.Message);
                }
            }

            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitTransactionAsync(ct);

            return processedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during outbox batch processing transaction. Rolling back.");
            await _unitOfWork.RollbackTransactionAsync(ct);
            throw;
        }
    }
}
