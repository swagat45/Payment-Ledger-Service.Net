using System.Text.Json;
using Microsoft.Extensions.Logging;
using PaymentLedger.Business.Events;
using PaymentLedger.Repository.Entities;
using PaymentLedger.Repository.Repositories;

namespace PaymentLedger.Business.Common;

public sealed class EventPublisher : IEventPublisher
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    private readonly IOutboxRepository _outboxRepository;
    private readonly ILogger<EventPublisher> _logger;

    public EventPublisher(
        IOutboxRepository outboxRepository,
        ILogger<EventPublisher> logger)
    {
        _outboxRepository = outboxRepository;
        _logger = logger;
    }

    public async Task PublishAsync<TEvent>(TEvent domainEvent, CancellationToken ct = default)
        where TEvent : IDomainEvent
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        var eventType = domainEvent.GetType().Name;
        var payload = JsonSerializer.Serialize(domainEvent, JsonOptions);

        var outboxMessage = OutboxMessage.Create(
            type: eventType,
            payload: payload,
            occurredOnUtc: domainEvent.OccurredOnUtc);

        await _outboxRepository.AddAsync(outboxMessage, ct);

        _logger.LogDebug(
            "Staged outbox domain event '{EventType}' (MessageId: {MessageId}) for atomic commit.",
            eventType,
            outboxMessage.Id);
    }
}
