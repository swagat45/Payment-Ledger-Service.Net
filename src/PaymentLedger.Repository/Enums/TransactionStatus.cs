namespace PaymentLedger.Repository.Enums;

public enum TransactionStatus
{
    Pending = 1,
    Posted = 2,
    Failed = 3,
    Refunded = 4,
    PartiallyRefunded = 5,
    Reversed = 6
}
