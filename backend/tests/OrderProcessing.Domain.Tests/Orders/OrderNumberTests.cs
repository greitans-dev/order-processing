using OrderProcessing.Domain.Orders;
using Shouldly;

namespace OrderProcessing.Domain.Tests.Orders;

public class OrderNumberTests
{
    [Fact]
    public void OrderNumber_New_HasOrdPrefixAndEightCharSuffix()
    {
        var number = OrderNumber.New();
        number.Value.ShouldMatch("^ORD-[0-9A-F]{8}$");
    }

    [Fact]
    public void OrderNumber_New_ReturnsUniqueValues() =>
        OrderNumber.New().ShouldNotBe(OrderNumber.New());

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void OrderNumber_BlankValue_Throws(string value) =>
        Should.Throw<ArgumentException>(() => new OrderNumber(value));
}
