using PaymentLedger.Business.Events;

namespace PaymentLedger.Business.Common;

public interface IEventPublisher
{
    Task PublishAsync<TEvent>(TEvent domainEvent, CancellationToken ct = default)
        where TEvent : IDomainEvent;
}
