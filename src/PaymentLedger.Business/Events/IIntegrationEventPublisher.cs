namespace PaymentLedger.Business.Events;

public interface IIntegrationEventPublisher
{
    Task PublishAsync(Guid messageId, string eventType, string payload, DateTimeOffset occurredOnUtc, CancellationToken ct = default);
}
