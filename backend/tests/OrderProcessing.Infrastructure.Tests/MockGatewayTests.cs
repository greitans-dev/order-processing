using OrderProcessing.Application.Abstractions;
using OrderProcessing.Domain.Orders;
using OrderProcessing.Domain.Payments;
using OrderProcessing.Infrastructure.Payments;
using Shouldly;

namespace OrderProcessing.Infrastructure.Tests;

public class MockGatewayTests
{
    public static TheoryData<IPaymentGateway, string> Gateways => new()
    {
        { new MockGatewayAlpha(), "ALPHA-" },
        { new MockGatewayBeta(), "BETA-" },
    };

    private static PaymentRequest Request(decimal amount) =>
        new(OrderNumber.New(), Money.Of(amount, "EUR"), null);

    [Theory, MemberData(nameof(Gateways))]
    public async Task Charges_just_below_limit_succeed(IPaymentGateway gateway, string prefix)
    {
        var result = await gateway.ChargeAsync(Request(9999.99m), default);
        result.IsSuccess.ShouldBeTrue();
        result.ConfirmationCode.ShouldStartWith(prefix);
    }

    [Theory, MemberData(nameof(Gateways))]
    public async Task Charges_at_limit_are_declined_with_readable_reason(IPaymentGateway gateway, string _)
    {
        var result = await gateway.ChargeAsync(Request(10000.00m), default);
        result.IsSuccess.ShouldBeFalse();
        result.FailureReason.ShouldNotBeNullOrWhiteSpace();
        result.FailureReason.ShouldContain("limit");
    }

    [Fact]
    public void Gateways_have_expected_ids()
    {
        new MockGatewayAlpha().GatewayId.ShouldBe("mock-alpha");
        new MockGatewayBeta().GatewayId.ShouldBe("mock-beta");
    }
}
