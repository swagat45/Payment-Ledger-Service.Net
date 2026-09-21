using PaymentLedger.Repository.Entities;
using PaymentLedger.Repository.Enums;
using PaymentLedger.Repository.Exceptions;

namespace PaymentLedger.UnitTests.Domain;

public class LedgerEntryTests
{
    private readonly DateTimeOffset _now = DateTimeOffset.UtcNow;
    private readonly Guid _txnId = Guid.NewGuid();
    private readonly Guid _walletId = Guid.NewGuid();

    [Fact]
    public void Create_WithValidInputs_ShouldInitializeLedgerEntryCorrectly()
    {
        var entry = LedgerEntry.Create(
            _txnId,
            _walletId,
            LedgerEntryType.Credit,
            100m,
            "USD",
            250m,
            _now);

        Assert.Equal(_txnId, entry.TransactionId);
        Assert.Equal(_walletId, entry.WalletId);
        Assert.Equal(LedgerEntryType.Credit, entry.EntryType);
        Assert.Equal(100m, entry.Amount);
        Assert.Equal("USD", entry.Currency);
        Assert.Equal(250m, entry.BalanceAfterTransaction);
        Assert.Equal(_now, entry.CreatedAt);
    }

    [Fact]
    public void Create_ForTreasuryDebit_ShouldAllowNullBalanceAfterTransaction()
    {
        var entry = LedgerEntry.Create(
            _txnId,
            _walletId,
            LedgerEntryType.Debit,
            100m,
            "USD",
            null,
            _now);

        Assert.Null(entry.BalanceAfterTransaction);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void Create_WithInvalidAmount_ShouldThrowDomainException(decimal amount)
    {
        Assert.Throws<DomainException>(() => LedgerEntry.Create(
            _txnId,
            _walletId,
            LedgerEntryType.Debit,
            amount,
            "USD",
            null,
            _now));
    }

    [Fact]
    public void Create_WithEmptyTransactionId_ShouldThrowDomainException()
    {
        Assert.Throws<DomainException>(() => LedgerEntry.Create(
            Guid.Empty,
            _walletId,
            LedgerEntryType.Debit,
            50m,
            "USD",
            null,
            _now));
    }

    [Fact]
    public void Create_WithEmptyWalletId_ShouldThrowDomainException()
    {
        Assert.Throws<DomainException>(() => LedgerEntry.Create(
            _txnId,
            Guid.Empty,
            LedgerEntryType.Debit,
            50m,
            "USD",
            null,
            _now));
    }
}
