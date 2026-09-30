using OrderProcessing.Domain.Orders;
using OrderProcessing.Domain.Payments;
using Shouldly;

namespace OrderProcessing.Domain.Tests;

public class ValueObjectTests
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

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void PaymentGatewayId_BlankValue_Throws(string value) =>
        Should.Throw<ArgumentException>(() => new PaymentGatewayId(value));

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("has\nnewline")]
    public void IdempotencyKey_BlankOrControlCharacters_Throws(string value) =>
        Should.Throw<ArgumentException>(() => new IdempotencyKey(value));

    [Fact]
    public void IdempotencyKey_Over255Characters_Throws() =>
        Should.Throw<ArgumentException>(() => new IdempotencyKey(new string('a', 256)));

    [Fact]
    public void IdempotencyKey_Exactly255Characters_IsAccepted() =>
        new IdempotencyKey(new string('a', 255)).Value.Length.ShouldBe(255);
}
