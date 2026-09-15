using PaymentLedger.Application.Wallets.Commands.DebitWallet;

namespace PaymentLedger.UnitTests.Application.Wallets;

public class DebitWalletValidatorTests
{
    private readonly DebitWalletCommandValidator _validator = new();

    [Fact]
    public void Validate_WithValidCommand_ShouldPass()
    {
        var command = new DebitWalletCommand(
            Guid.NewGuid(),
            50.25m,
            "USD",
            "idem-debit-123",
            "Merchant payment",
            "{\"orderId\":\"ORD123\"}");

        var result = _validator.Validate(command);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_WithEmptyWalletId_ShouldFail()
    {
        var command = new DebitWalletCommand(
            Guid.Empty,
            50m,
            "USD",
            "idem-debit-123");

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(DebitWalletCommand.WalletId));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void Validate_WithInvalidAmount_ShouldFail(decimal amount)
    {
        var command = new DebitWalletCommand(
            Guid.NewGuid(),
            amount,
            "USD",
            "idem-debit-123");

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(DebitWalletCommand.Amount));
    }

    [Theory]
    [InlineData("")]
    [InlineData("US")]
    [InlineData("USDT")]
    [InlineData("12A")]
    public void Validate_WithInvalidCurrency_ShouldFail(string currency)
    {
        var command = new DebitWalletCommand(
            Guid.NewGuid(),
            50m,
            currency,
            "idem-debit-123");

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(DebitWalletCommand.Currency));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WithEmptyIdempotencyKey_ShouldFail(string key)
    {
        var command = new DebitWalletCommand(
            Guid.NewGuid(),
            50m,
            "USD",
            key);

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(DebitWalletCommand.IdempotencyKey));
    }

    [Fact]
    public void Validate_WithInvalidJsonMetadata_ShouldFail()
    {
        var command = new DebitWalletCommand(
            Guid.NewGuid(),
            50m,
            "USD",
            "idem-debit-123",
            "Merchant payment",
            "not-a-valid-json");

        var result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(DebitWalletCommand.Metadata));
    }
}
