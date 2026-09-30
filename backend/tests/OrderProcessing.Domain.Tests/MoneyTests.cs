using OrderProcessing.Domain.Payments;
using Shouldly;

namespace OrderProcessing.Domain.Tests;

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

    [Fact]
    public void Of_UnsupportedCurrency_Throws() =>
        Should.Throw<UnsupportedCurrencyException>(() => Money.Of(1m, "USD"));

    [Fact]
    public void All_Default_ContainsEuro() =>
        SupportedCurrencies.All.ShouldContain(c => c.Code == "EUR" && c.Name == "Euro");
}
