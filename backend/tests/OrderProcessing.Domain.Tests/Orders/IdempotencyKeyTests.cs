using OrderProcessing.Domain.Orders;
using Shouldly;

namespace OrderProcessing.Domain.Tests.Orders;

public class IdempotencyKeyTests
{
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
