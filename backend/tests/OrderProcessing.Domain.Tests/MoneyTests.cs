using OrderProcessing.Domain.Payments;
using Shouldly;

namespace OrderProcessing.Domain.Tests;

public class MoneyTests
{
    [Fact]
    public void Of_accepts_lowercase_currency_and_normalizes_it()
    {
        var money = Money.Of(10m, "eur");
        money.CurrencyCode.ShouldBe("EUR");
        money.Amount.ShouldBe(10m);
    }

    [Fact]
    public void Of_rejects_negative_amount() =>
        Should.Throw<ArgumentOutOfRangeException>(() => Money.Of(-0.01m, "EUR"));

    [Fact]
    public void Of_rejects_unsupported_currency() =>
        Should.Throw<UnsupportedCurrencyException>(() => Money.Of(1m, "USD"));

    [Fact]
    public void SupportedCurrencies_lists_euro() =>
        SupportedCurrencies.All.ShouldContain(c => c.Code == "EUR" && c.Name == "Euro");
}
