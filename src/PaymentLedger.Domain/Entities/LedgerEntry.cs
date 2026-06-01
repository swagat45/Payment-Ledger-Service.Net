using PaymentLedger.Domain.Enums;

namespace PaymentLedger.Domain.Entities;

public class LedgerEntry
{

    public long Id { get; private set; }

    public Guid TransactionId { get; private set; }

    public Guid WalletId { get; private set; }

    public LedgerEntryType EntryType { get; private set; }

    public decimal Amount { get; private set; }

    public string Currency { get; private set; } = string.Empty;

    public decimal? BalanceAfterTransaction { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
}
