using OrderProcessing.Domain.Orders;
using OrderProcessing.Domain.Payments;
using Shouldly;

namespace OrderProcessing.Domain.Tests;

public class ValueObjectTests
{
    [Fact]
    public void OrderNumber_New_has_ORD_prefix_and_8_char_suffix()
    {
        var number = OrderNumber.New();
        number.Value.ShouldMatch("^ORD-[0-9A-F]{8}$");
    }

    [Fact]
    public void OrderNumber_New_is_unique() =>
        OrderNumber.New().ShouldNotBe(OrderNumber.New());

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void OrderNumber_rejects_blank(string value) =>
        Should.Throw<ArgumentException>(() => new OrderNumber(value));

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void PaymentGatewayId_rejects_blank(string value) =>
        Should.Throw<ArgumentException>(() => new PaymentGatewayId(value));

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("has\nnewline")]
    public void IdempotencyKey_rejects_blank_or_control_characters(string value) =>
        Should.Throw<ArgumentException>(() => new IdempotencyKey(value));

    [Fact]
    public void IdempotencyKey_rejects_over_255_characters() =>
        Should.Throw<ArgumentException>(() => new IdempotencyKey(new string('a', 256)));

    [Fact]
    public void IdempotencyKey_accepts_255_characters() =>
        new IdempotencyKey(new string('a', 255)).Value.Length.ShouldBe(255);
}
