using PaymentLedger.Application.Wallets.Commands.BlockWallet;
using PaymentLedger.Application.Wallets.Commands.CloseWallet;
using PaymentLedger.Application.Wallets.Commands.CreateWallet;
using PaymentLedger.Application.Wallets.Commands.UnblockWallet;
using PaymentLedger.Application.Wallets.Queries.GetWalletBalance;
using PaymentLedger.Application.Wallets.Queries.GetWalletById;
using PaymentLedger.Application.Wallets.Queries.GetWalletsByUserId;

namespace PaymentLedger.UnitTests.Application.Wallets;

public class WalletValidatorTests
{
    [Fact]
    public void CreateWalletCommandValidator_ShouldValidateCorrectly()
    {
        var validator = new CreateWalletCommandValidator();

        var validCommand = new CreateWalletCommand(Guid.NewGuid(), "USD");
        var invalidUserCommand = new CreateWalletCommand(Guid.Empty, "USD");
        var invalidCurrencyCommand1 = new CreateWalletCommand(Guid.NewGuid(), "US");
        var invalidCurrencyCommand2 = new CreateWalletCommand(Guid.NewGuid(), "USDT");
        var invalidCurrencyCommand3 = new CreateWalletCommand(Guid.NewGuid(), "12A");

        Assert.True(validator.Validate(validCommand).IsValid);
        Assert.False(validator.Validate(invalidUserCommand).IsValid);
        Assert.False(validator.Validate(invalidCurrencyCommand1).IsValid);
        Assert.False(validator.Validate(invalidCurrencyCommand2).IsValid);
        Assert.False(validator.Validate(invalidCurrencyCommand3).IsValid);
    }

    [Fact]
    public void GetWalletBalanceQueryValidator_ShouldValidateCorrectly()
    {
        var validator = new GetWalletBalanceQueryValidator();

        Assert.True(validator.Validate(new GetWalletBalanceQuery(Guid.NewGuid())).IsValid);
        Assert.False(validator.Validate(new GetWalletBalanceQuery(Guid.Empty)).IsValid);
    }

    [Fact]
    public void GetWalletByIdQueryValidator_ShouldValidateCorrectly()
    {
        var validator = new GetWalletByIdQueryValidator();

        Assert.True(validator.Validate(new GetWalletByIdQuery(Guid.NewGuid())).IsValid);
        Assert.False(validator.Validate(new GetWalletByIdQuery(Guid.Empty)).IsValid);
    }

    [Fact]
    public void GetWalletsByUserIdQueryValidator_ShouldValidateCorrectly()
    {
        var validator = new GetWalletsByUserIdQueryValidator();

        Assert.True(validator.Validate(new GetWalletsByUserIdQuery(Guid.NewGuid())).IsValid);
        Assert.False(validator.Validate(new GetWalletsByUserIdQuery(Guid.Empty)).IsValid);
    }

    [Fact]
    public void LifecycleCommandValidators_ShouldValidateCorrectly()
    {
        var blockValidator = new BlockWalletCommandValidator();
        var unblockValidator = new UnblockWalletCommandValidator();
        var closeValidator = new CloseWalletCommandValidator();

        Assert.True(blockValidator.Validate(new BlockWalletCommand(Guid.NewGuid())).IsValid);
        Assert.False(blockValidator.Validate(new BlockWalletCommand(Guid.Empty)).IsValid);

        Assert.True(unblockValidator.Validate(new UnblockWalletCommand(Guid.NewGuid())).IsValid);
        Assert.False(unblockValidator.Validate(new UnblockWalletCommand(Guid.Empty)).IsValid);

        Assert.True(closeValidator.Validate(new CloseWalletCommand(Guid.NewGuid())).IsValid);
        Assert.False(closeValidator.Validate(new CloseWalletCommand(Guid.Empty)).IsValid);
    }
}
