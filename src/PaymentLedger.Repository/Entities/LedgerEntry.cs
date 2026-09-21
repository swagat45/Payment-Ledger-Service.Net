using PaymentLedger.Repository.Enums;

namespace PaymentLedger.Repository.Entities;

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

    public LedgerEntry()
    {
    }

    public static LedgerEntry Create(
        Guid transactionId,
        Guid walletId,
        LedgerEntryType entryType,
        decimal amount,
        string currency,
        decimal? balanceAfterTransaction,
        DateTimeOffset timestamp)
    {
        if (transactionId == Guid.Empty)
        {
            throw new PaymentLedger.Repository.Exceptions.DomainException("Transaction ID is required.");
        }

        if (walletId == Guid.Empty)
        {
            throw new PaymentLedger.Repository.Exceptions.DomainException("Wallet ID is required.");
        }

        if (amount <= 0)
        {
            throw new PaymentLedger.Repository.Exceptions.DomainException("Ledger entry amount must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(currency))
        {
            throw new PaymentLedger.Repository.Exceptions.DomainException("Currency is required.");
        }

        return new LedgerEntry
        {
            TransactionId = transactionId,
            WalletId = walletId,
            EntryType = entryType,
            Amount = decimal.Round(amount, 2, MidpointRounding.ToEven),
            Currency = currency.Trim().ToUpperInvariant(),
            BalanceAfterTransaction = balanceAfterTransaction.HasValue
                ? decimal.Round(balanceAfterTransaction.Value, 2, MidpointRounding.ToEven)
                : null,
            CreatedAt = timestamp
        };
    }
}
