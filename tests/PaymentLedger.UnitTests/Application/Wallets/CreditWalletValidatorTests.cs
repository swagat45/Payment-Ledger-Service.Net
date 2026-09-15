using PaymentLedger.Application.Wallets.Commands.CreditWallet;

namespace PaymentLedger.UnitTests.Application.Wallets;

public class CreditWalletValidatorTests
{
    private readonly CreditWalletCommandValidator _validator = new();

    [Fact]
    public void Validate_WithValidCommand_ShouldPass()
    {
        var command = new CreditWalletCommand(
            Guid.NewGuid(),
            100.50m,
            "USD",
            "idem-key-123",
            "Top up wallet",
            "{\"source\":\"card\"}");

        var result = _validator.Validate(command);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_WithEmptyWalletId_ShouldFail()
    {
        var command = new CreditWalletCommand(
            Guid.Empty,
            100m,
            "USD",
            "idem-key-123");

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreditWalletCommand.WalletId));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-50)]
    public void Validate_WithInvalidAmount_ShouldFail(decimal amount)
    {
        var command = new CreditWalletCommand(
            Guid.NewGuid(),
            amount,
            "USD",
            "idem-key-123");

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreditWalletCommand.Amount));
    }

    [Theory]
    [InlineData("")]
    [InlineData("US")]
    [InlineData("USDT")]
    [InlineData("12A")]
    public void Validate_WithInvalidCurrency_ShouldFail(string currency)
    {
        var command = new CreditWalletCommand(
            Guid.NewGuid(),
            100m,
            currency,
            "idem-key-123");

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreditWalletCommand.Currency));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WithEmptyIdempotencyKey_ShouldFail(string key)
    {
        var command = new CreditWalletCommand(
            Guid.NewGuid(),
            100m,
            "USD",
            key);

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreditWalletCommand.IdempotencyKey));
    }

    [Fact]
    public void Validate_WithInvalidJsonMetadata_ShouldFail()
    {
        var command = new CreditWalletCommand(
            Guid.NewGuid(),
            100m,
            "USD",
            "idem-key-123",
            "Top up",
            "invalid-json-content");

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(CreditWalletCommand.Metadata));
    }
}
