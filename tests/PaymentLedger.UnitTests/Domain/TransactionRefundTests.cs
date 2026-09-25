using PaymentLedger.Repository.Entities;
using PaymentLedger.Repository.Enums;
using PaymentLedger.Repository.Exceptions;
using PaymentLedger.Shared.Constants;

namespace PaymentLedger.UnitTests.Domain;

public class TransactionRefundTests
{
    private readonly DateTimeOffset _now = DateTimeOffset.UtcNow;
    private readonly Guid _originalTxnId = Guid.NewGuid();
    private readonly Guid _treasuryWalletId = Guid.NewGuid();
    private readonly Guid _userWalletId = Guid.NewGuid();

    [Fact]
    public void CreateRefund_WithValidParameters_InitializesTransactionCorrectly()
    {
        // Act
        var refundTxn = Transaction.CreateRefund(
            originalTransactionId: _originalTxnId,
            sourceWalletId: _treasuryWalletId,
            destinationWalletId: _userWalletId,
            amount: 49.99m,
            currency: "USD",
            idempotencyKey: "refund-key-001",
            timestamp: _now,
            description: "Product return refund",
            metadata: "{\"orderId\":\"12345\"}");

        // Assert
        Assert.NotEqual(Guid.Empty, refundTxn.Id);
        Assert.StartsWith("TXN-RF-", refundTxn.TransactionCode);
        Assert.Equal(TransactionType.Refund, refundTxn.Type);
        Assert.Equal(TransactionStatus.Posted, refundTxn.Status);
        Assert.Equal(_treasuryWalletId, refundTxn.SourceWalletId);
        Assert.Equal(_userWalletId, refundTxn.DestinationWalletId);
        Assert.Equal(49.99m, refundTxn.Amount);
        Assert.Equal("USD", refundTxn.Currency);
        Assert.Equal("refund-key-001", refundTxn.IdempotencyKey);
        Assert.Equal(_originalTxnId, refundTxn.ReferenceTransactionId);
        Assert.Equal("Product return refund", refundTxn.Description);
        Assert.Equal("{\"orderId\":\"12345\"}", refundTxn.Metadata);
        Assert.Equal(_now, refundTxn.CreatedAt);
        Assert.Equal(_now, refundTxn.UpdatedAt);
    }

    [Fact]
    public void CreateRefund_WithEmptyOriginalTransactionId_ThrowsDomainException()
    {
        var ex = Assert.Throws<DomainException>(() => Transaction.CreateRefund(
            originalTransactionId: Guid.Empty,
            sourceWalletId: _treasuryWalletId,
            destinationWalletId: _userWalletId,
            amount: 50m,
            currency: "USD",
            idempotencyKey: "key-1",
            timestamp: _now));

        Assert.Contains("Original transaction ID is required", ex.Message);
    }

    [Fact]
    public void CreateRefund_WithEmptySourceWalletId_ThrowsDomainException()
    {
        var ex = Assert.Throws<DomainException>(() => Transaction.CreateRefund(
            originalTransactionId: _originalTxnId,
            sourceWalletId: Guid.Empty,
            destinationWalletId: _userWalletId,
            amount: 50m,
            currency: "USD",
            idempotencyKey: "key-1",
            timestamp: _now));

        Assert.Contains("Source wallet ID is required", ex.Message);
    }

    [Fact]
    public void CreateRefund_WithEmptyDestinationWalletId_ThrowsDomainException()
    {
        var ex = Assert.Throws<DomainException>(() => Transaction.CreateRefund(
            originalTransactionId: _originalTxnId,
            sourceWalletId: _treasuryWalletId,
            destinationWalletId: Guid.Empty,
            amount: 50m,
            currency: "USD",
            idempotencyKey: "key-1",
            timestamp: _now));

        Assert.Contains("Destination wallet ID is required", ex.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    [InlineData(-0.01)]
    public void CreateRefund_WithInvalidAmount_ThrowsDomainException(decimal amount)
    {
        var ex = Assert.Throws<DomainException>(() => Transaction.CreateRefund(
            originalTransactionId: _originalTxnId,
            sourceWalletId: _treasuryWalletId,
            destinationWalletId: _userWalletId,
            amount: amount,
            currency: "USD",
            idempotencyKey: "key-1",
            timestamp: _now));

        Assert.Equal(ErrorCodes.InvalidAmount, ex.ErrorCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("US")]
    [InlineData("USDT")]
    [InlineData("123")]
    public void CreateRefund_WithInvalidCurrency_ThrowsDomainException(string currency)
    {
        var ex = Assert.Throws<DomainException>(() => Transaction.CreateRefund(
            originalTransactionId: _originalTxnId,
            sourceWalletId: _treasuryWalletId,
            destinationWalletId: _userWalletId,
            amount: 50m,
            currency: currency,
            idempotencyKey: "key-1",
            timestamp: _now));

        Assert.Equal(ErrorCodes.InvalidCurrency, ex.ErrorCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void CreateRefund_WithInvalidIdempotencyKey_ThrowsDomainException(string? key)
    {
        var ex = Assert.Throws<DomainException>(() => Transaction.CreateRefund(
            originalTransactionId: _originalTxnId,
            sourceWalletId: _treasuryWalletId,
            destinationWalletId: _userWalletId,
            amount: 50m,
            currency: "USD",
            idempotencyKey: key!,
            timestamp: _now));

        Assert.Equal(ErrorCodes.IdempotencyKeyRequired, ex.ErrorCode);
    }

    [Fact]
    public void MarkRefunded_WhenPosted_TransitionsToRefunded()
    {
        var txn = Transaction.CreateDebit(
            _userWalletId,
            _treasuryWalletId,
            100m,
            "USD",
            "key-1",
            _now);

        var refundTime = _now.AddMinutes(5);
        txn.MarkRefunded(refundTime);

        Assert.Equal(TransactionStatus.Refunded, txn.Status);
        Assert.Equal(refundTime, txn.UpdatedAt);
    }

    [Fact]
    public void MarkRefunded_WhenPartiallyRefunded_TransitionsToRefunded()
    {
        var txn = Transaction.CreateDebit(
            _userWalletId,
            _treasuryWalletId,
            100m,
            "USD",
            "key-1",
            _now);

        txn.MarkPartiallyRefunded(_now.AddMinutes(2));
        var refundTime = _now.AddMinutes(5);
        txn.MarkRefunded(refundTime);

        Assert.Equal(TransactionStatus.Refunded, txn.Status);
        Assert.Equal(refundTime, txn.UpdatedAt);
    }

    [Fact]
    public void MarkPartiallyRefunded_WhenPosted_TransitionsToPartiallyRefunded()
    {
        var txn = Transaction.CreateDebit(
            _userWalletId,
            _treasuryWalletId,
            100m,
            "USD",
            "key-1",
            _now);

        var refundTime = _now.AddMinutes(2);
        txn.MarkPartiallyRefunded(refundTime);

        Assert.Equal(TransactionStatus.PartiallyRefunded, txn.Status);
        Assert.Equal(refundTime, txn.UpdatedAt);
    }

    [Fact]
    public void MarkRefunded_WhenAlreadyRefunded_ThrowsDomainException()
    {
        var txn = Transaction.CreateDebit(
            _userWalletId,
            _treasuryWalletId,
            100m,
            "USD",
            "key-1",
            _now);

        txn.MarkRefunded(_now.AddMinutes(1));

        var ex = Assert.Throws<DomainException>(() => txn.MarkRefunded(_now.AddMinutes(2)));
        Assert.Equal(ErrorCodes.TransactionNotRefundable, ex.ErrorCode);
    }

    [Fact]
    public void MarkPartiallyRefunded_WhenAlreadyRefunded_ThrowsDomainException()
    {
        var txn = Transaction.CreateDebit(
            _userWalletId,
            _treasuryWalletId,
            100m,
            "USD",
            "key-1",
            _now);

        txn.MarkRefunded(_now.AddMinutes(1));

        var ex = Assert.Throws<DomainException>(() => txn.MarkPartiallyRefunded(_now.AddMinutes(2)));
        Assert.Equal(ErrorCodes.TransactionNotRefundable, ex.ErrorCode);
    }
}
