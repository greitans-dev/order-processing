using OrderProcessing.Domain.Payments;
using Shouldly;

namespace OrderProcessing.Domain.Tests.Payments;

public class MoneyTests
{
    [Fact]
    public void Of_LowercaseCurrency_NormalizesToUppercase()
    {
        var money = Money.Of(10m, "eur");
        money.CurrencyCode.ShouldBe("EUR");
        money.Amount.ShouldBe(10m);
    }

    [Fact]
    public void Of_NegativeAmount_Throws() =>
        Should.Throw<ArgumentOutOfRangeException>(() => Money.Of(-0.01m, "EUR"));

    [Theory]
    [InlineData("0")]
    [InlineData("0.00")]
    public void Of_ZeroAmount_Throws(string amount) =>
        Should.Throw<ArgumentOutOfRangeException>(() => Money.Of(decimal.Parse(amount), "EUR"));

    [Theory]
    [InlineData("1.999")]
    [InlineData("0.001")]
    public void Of_MoreThanTwoDecimalPlaces_Throws(string amount) =>
        Should.Throw<ArgumentOutOfRangeException>(() => Money.Of(decimal.Parse(amount), "EUR"));

    [Theory]
    [InlineData("0.01")]
    [InlineData("10")]
    [InlineData("10.50")]
    [InlineData("10.500")] // trailing zeros do not add precision
    public void Of_AtMostTwoDecimalPlaces_Succeeds(string amount) =>
        Money.Of(decimal.Parse(amount), "EUR").Amount.ShouldBe(decimal.Parse(amount));

    [Fact]
    public void Of_UnsupportedCurrency_Throws() =>
        Should.Throw<UnsupportedCurrencyException>(() => Money.Of(1m, "USD"));
}
