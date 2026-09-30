using OrderProcessing.Domain.Payments;
using Shouldly;

namespace OrderProcessing.Domain.Tests.Payments;

public class SupportedCurrenciesTests
{
    [Fact]
    public void All_Default_ContainsEuro() =>
        SupportedCurrencies.All.ShouldContain(c => c.Code == "EUR" && c.Name == "Euro");
}
