using OrderProcessing.Domain.Payments;
using Shouldly;

namespace OrderProcessing.Domain.Tests.Payments;

public class PaymentGatewayIdTests
{
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void PaymentGatewayId_BlankValue_Throws(string value) =>
        Should.Throw<ArgumentException>(() => new PaymentGatewayId(value));
}
