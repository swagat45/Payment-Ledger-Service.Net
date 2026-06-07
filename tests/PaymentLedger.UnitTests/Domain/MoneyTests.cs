using PaymentLedger.Domain.ValueObjects;

namespace PaymentLedger.UnitTests.Domain;

public sealed class MoneyTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_ShouldThrow_WhenAmountIsNotPositive(decimal amount)
    {
        Action act = () => _ = new Money(amount, "USD");

        var exception = Assert.Throws<ArgumentOutOfRangeException>(act);
        Assert.Equal("amount", exception.ParamName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_ShouldThrow_WhenCurrencyIsMissing(string? currency)
    {
        Action act = () => _ = new Money(10m, currency!);

        var exception = Assert.Throws<ArgumentException>(act);
        Assert.Equal("currency", exception.ParamName);
    }

    [Theory]
    [InlineData("US")]
    [InlineData("USDD")]
    public void Constructor_ShouldThrow_WhenCurrencyLengthIsNotThreeLetters(string currency)
    {
        Action act = () => _ = new Money(10m, currency);

        var exception = Assert.Throws<ArgumentException>(act);
        Assert.Equal("currency", exception.ParamName);
    }

    [Theory]
    [InlineData("U1D")]
    [InlineData("U-D")]
    public void Constructor_ShouldThrow_WhenCurrencyContainsNonLetters(string currency)
    {
        Action act = () => _ = new Money(10m, currency);

        var exception = Assert.Throws<ArgumentException>(act);
        Assert.Equal("currency", exception.ParamName);
    }

    [Fact]
    public void Constructor_ShouldNormalizeCurrencyAndRoundAmountToTwoDecimals()
    {
        var money = new Money(10.125m, " usd ");

        Assert.Equal(10.12m, money.Amount);
        Assert.Equal("USD", money.Currency);
    }

    [Fact]
    public void ToString_ShouldReturnCurrencyAndTwoDecimalAmount()
    {
        var money = new Money(10.135m, "usd");

        Assert.Equal("USD 10.14", money.ToString());
    }
}
