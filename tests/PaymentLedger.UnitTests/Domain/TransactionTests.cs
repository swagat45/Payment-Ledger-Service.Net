using PaymentLedger.Domain.Entities;
using PaymentLedger.Domain.Enums;
using PaymentLedger.Domain.Exceptions;

namespace PaymentLedger.UnitTests.Domain;

public class TransactionTests
{
    private readonly DateTimeOffset _now = DateTimeOffset.UtcNow;
    private readonly Guid _sourceWalletId = Guid.NewGuid();
    private readonly Guid _destWalletId = Guid.NewGuid();

    [Fact]
    public void CreateCredit_WithValidInputs_ShouldInitializeTransactionCorrectly()
    {
        // Act
        var txn = Transaction.CreateCredit(
            _sourceWalletId,
            _destWalletId,
            150.75m,
            "USD",
            "idem-key-123",
            _now,
            "Test credit",
            "{\"source\":\"card\"}");

        // Assert
        Assert.NotEqual(Guid.Empty, txn.Id);
        Assert.StartsWith("TXN-CR-", txn.TransactionCode);
        Assert.Equal(TransactionType.Credit, txn.Type);
        Assert.Equal(TransactionStatus.Posted, txn.Status);
        Assert.Equal(_sourceWalletId, txn.SourceWalletId);
        Assert.Equal(_destWalletId, txn.DestinationWalletId);
        Assert.Equal(150.75m, txn.Amount);
        Assert.Equal("USD", txn.Currency);
        Assert.Equal("idem-key-123", txn.IdempotencyKey);
        Assert.Equal("Test credit", txn.Description);
        Assert.Equal("{\"source\":\"card\"}", txn.Metadata);
        Assert.Equal(_now, txn.CreatedAt);
        Assert.Equal(_now, txn.UpdatedAt);
    }

    [Fact]
    public void CreateCredit_WithEmptySourceWallet_ShouldThrowDomainException()
    {
        Assert.Throws<DomainException>(() => Transaction.CreateCredit(
            Guid.Empty,
            _destWalletId,
            100m,
            "USD",
            "idem-1",
            _now));
    }

    [Fact]
    public void CreateCredit_WithEmptyDestinationWallet_ShouldThrowDomainException()
    {
        Assert.Throws<DomainException>(() => Transaction.CreateCredit(
            _sourceWalletId,
            Guid.Empty,
            100m,
            "USD",
            "idem-1",
            _now));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-50)]
    public void CreateCredit_WithInvalidAmount_ShouldThrowDomainException(decimal amount)
    {
        Assert.Throws<DomainException>(() => Transaction.CreateCredit(
            _sourceWalletId,
            _destWalletId,
            amount,
            "USD",
            "idem-1",
            _now));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void CreateCredit_WithInvalidCurrency_ShouldThrowDomainException(string? currency)
    {
        Assert.Throws<DomainException>(() => Transaction.CreateCredit(
            _sourceWalletId,
            _destWalletId,
            100m,
            currency!,
            "idem-1",
            _now));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void CreateCredit_WithInvalidIdempotencyKey_ShouldThrowDomainException(string? key)
    {
        Assert.Throws<DomainException>(() => Transaction.CreateCredit(
            _sourceWalletId,
            _destWalletId,
            100m,
            "USD",
            key!,
            _now));
    }

    [Fact]
    public void CreateDebit_WithValidInputs_ShouldInitializeTransactionCorrectly()
    {
        var txn = Transaction.CreateDebit(
            _sourceWalletId,
            _destWalletId,
            75.25m,
            "USD",
            "idem-key-debit",
            _now,
            "Payment",
            "{\"orderId\":\"123\"}");

        Assert.NotEqual(Guid.Empty, txn.Id);
        Assert.StartsWith("TXN-DB-", txn.TransactionCode);
        Assert.Equal(TransactionType.Debit, txn.Type);
        Assert.Equal(TransactionStatus.Posted, txn.Status);
        Assert.Equal(_sourceWalletId, txn.SourceWalletId);
        Assert.Equal(_destWalletId, txn.DestinationWalletId);
        Assert.Equal(75.25m, txn.Amount);
        Assert.Equal("USD", txn.Currency);
        Assert.Equal("idem-key-debit", txn.IdempotencyKey);
        Assert.Equal("Payment", txn.Description);
        Assert.Equal("{\"orderId\":\"123\"}", txn.Metadata);
        Assert.Equal(_now, txn.CreatedAt);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-25)]
    public void CreateDebit_WithInvalidAmount_ShouldThrowDomainException(decimal amount)
    {
        Assert.Throws<DomainException>(() => Transaction.CreateDebit(
            _sourceWalletId,
            _destWalletId,
            amount,
            "USD",
            "idem-key-debit",
            _now));
    }
}
