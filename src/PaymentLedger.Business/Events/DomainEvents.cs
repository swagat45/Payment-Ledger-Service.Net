namespace PaymentLedger.Business.Events;

public interface IDomainEvent
{
    DateTimeOffset OccurredOnUtc { get; }
}

public sealed record WalletCreatedEvent(
    Guid WalletId,
    Guid UserId,
    string Currency,
    DateTimeOffset OccurredOnUtc) : IDomainEvent;

public sealed record TransactionPostedEvent(
    Guid TransactionId,
    string TransactionCode,
    string Type,
    Guid? SourceWalletId,
    Guid? DestinationWalletId,
    decimal Amount,
    string Currency,
    string? Description,
    DateTimeOffset OccurredOnUtc) : IDomainEvent;

public sealed record WalletStatusChangedEvent(
    Guid WalletId,
    string PreviousStatus,
    string NewStatus,
    DateTimeOffset OccurredOnUtc) : IDomainEvent;
