using PaymentLedger.Domain.Entities;
using PaymentLedger.Domain.Enums;
using PaymentLedger.Domain.Exceptions;

namespace PaymentLedger.UnitTests.Domain;

public class WalletTests
{
    private readonly DateTimeOffset _now = DateTimeOffset.UtcNow;
    private readonly Guid _userId = Guid.NewGuid();

    [Fact]
    public void Create_WithValidInputs_ShouldInitializeWalletCorrectly()
    {
        // Act
        var wallet = Wallet.Create(_userId, "USD", _now);

        // Assert
        Assert.NotEqual(Guid.Empty, wallet.Id);
        Assert.Equal(_userId, wallet.UserId);
        Assert.Equal("USD", wallet.Currency);
        Assert.Equal(0m, wallet.Balance);
        Assert.Equal(WalletStatus.Active, wallet.Status);
        Assert.Equal(_now, wallet.CreatedAt);
        Assert.Equal(_now, wallet.UpdatedAt);
    }

    [Fact]
    public void Create_WithEmptyUserId_ShouldThrowDomainException()
    {
        Assert.Throws<DomainException>(() => Wallet.Create(Guid.Empty, "USD", _now));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("US")]
    [InlineData("USDT")]
    [InlineData("12A")]
    [InlineData("U$D")]
    public void Create_WithInvalidCurrency_ShouldThrowDomainException(string invalidCurrency)
    {
        Assert.Throws<DomainException>(() => Wallet.Create(_userId, invalidCurrency, _now));
    }

    [Fact]
    public void Block_WhenActive_ShouldSetStatusToBlocked()
    {
        var wallet = Wallet.Create(_userId, "USD", _now);
        var blockTime = _now.AddMinutes(5);

        wallet.Block(blockTime);

        Assert.Equal(WalletStatus.Blocked, wallet.Status);
        Assert.Equal(blockTime, wallet.UpdatedAt);
    }

    [Fact]
    public void Block_WhenClosed_ShouldThrowDomainException()
    {
        var wallet = Wallet.Create(_userId, "USD", _now);
        wallet.Close(_now);

        Assert.Throws<DomainException>(() => wallet.Block(_now.AddMinutes(1)));
    }

    [Fact]
    public void Unblock_WhenBlocked_ShouldSetStatusToActive()
    {
        var wallet = Wallet.Create(_userId, "USD", _now);
        wallet.Block(_now);
        var unblockTime = _now.AddMinutes(5);

        wallet.Unblock(unblockTime);

        Assert.Equal(WalletStatus.Active, wallet.Status);
        Assert.Equal(unblockTime, wallet.UpdatedAt);
    }

    [Fact]
    public void Unblock_WhenClosed_ShouldThrowDomainException()
    {
        var wallet = Wallet.Create(_userId, "USD", _now);
        wallet.Close(_now);

        Assert.Throws<DomainException>(() => wallet.Unblock(_now.AddMinutes(1)));
    }

    [Fact]
    public void Close_WhenBalanceIsZero_ShouldSetStatusToClosed()
    {
        var wallet = Wallet.Create(_userId, "USD", _now);
        var closeTime = _now.AddMinutes(5);

        wallet.Close(closeTime);

        Assert.Equal(WalletStatus.Closed, wallet.Status);
        Assert.Equal(closeTime, wallet.UpdatedAt);
    }

    [Fact]
    public void Close_WhenBalanceIsNonZero_ShouldThrowDomainException()
    {
        var wallet = Wallet.Create(_userId, "USD", _now);
        wallet.Credit(100m, _now);

        Assert.Throws<DomainException>(() => wallet.Close(_now.AddMinutes(1)));
    }

    [Fact]
    public void Credit_WhenActive_ShouldIncreaseBalance()
    {
        var wallet = Wallet.Create(_userId, "USD", _now);
        var creditTime = _now.AddMinutes(2);

        wallet.Credit(150.50m, creditTime);

        Assert.Equal(150.50m, wallet.Balance);
        Assert.Equal(creditTime, wallet.UpdatedAt);
    }

    [Fact]
    public void Credit_WithZeroOrNegativeAmount_ShouldThrowDomainException()
    {
        var wallet = Wallet.Create(_userId, "USD", _now);

        Assert.Throws<DomainException>(() => wallet.Credit(0m, _now));
        Assert.Throws<DomainException>(() => wallet.Credit(-10m, _now));
    }

    [Fact]
    public void Credit_WhenBlockedOrClosed_ShouldThrowDomainException()
    {
        var wallet = Wallet.Create(_userId, "USD", _now);
        wallet.Block(_now);

        Assert.Throws<DomainException>(() => wallet.Credit(50m, _now));
    }

    [Fact]
    public void Debit_WhenActiveAndSufficientBalance_ShouldDecreaseBalance()
    {
        var wallet = Wallet.Create(_userId, "USD", _now);
        wallet.Credit(200m, _now);
        var debitTime = _now.AddMinutes(5);

        wallet.Debit(75m, debitTime);

        Assert.Equal(125m, wallet.Balance);
        Assert.Equal(debitTime, wallet.UpdatedAt);
    }

    [Fact]
    public void Debit_WithInsufficientBalance_ShouldThrowDomainException()
    {
        var wallet = Wallet.Create(_userId, "USD", _now);
        wallet.Credit(50m, _now);

        Assert.Throws<DomainException>(() => wallet.Debit(100m, _now));
    }

    [Fact]
    public void Debit_WhenBlockedOrClosed_ShouldThrowDomainException()
    {
        var wallet = Wallet.Create(_userId, "USD", _now);
        wallet.Credit(100m, _now);
        wallet.Block(_now);

        Assert.Throws<DomainException>(() => wallet.Debit(25m, _now));
    }
}
