using OrderProcessing.Application.Abstractions;
using OrderProcessing.Domain.Orders;
using OrderProcessing.Domain.Payments;
using OrderProcessing.Infrastructure.Payments;
using Shouldly;

namespace OrderProcessing.Infrastructure.Tests.Payments;

public class MockGatewayTests
{
    public static TheoryData<IPaymentGateway, string> Gateways => new()
    {
        { new MockGateway("mock-alpha", "Alpha", "ALPHA-"), "ALPHA-" },
        { new MockGateway("mock-beta", "Beta", "BETA-"), "BETA-" },
    };

    private static PaymentRequest Request(decimal amount) =>
        new(OrderNumber.New(), Money.Of(amount, "EUR"), null);

    [Theory, MemberData(nameof(Gateways))]
    public async Task ChargeAsync_AmountJustBelowLimit_Succeeds(IPaymentGateway gateway, string prefix)
    {
        var result = await gateway.ChargeAsync(Request(9999.99m), default);
        result.ShouldBeOfType<PaymentResult.Approved>().ConfirmationCode.ShouldStartWith(prefix);
    }

    [Theory, MemberData(nameof(Gateways))]
    public async Task ChargeAsync_AmountAtLimit_DeclinesWithReadableReason(IPaymentGateway gateway, string _)
    {
        var result = await gateway.ChargeAsync(Request(10000.00m), default);
        var reason = result.ShouldBeOfType<PaymentResult.Declined>().Reason;
        reason.ShouldNotBeNullOrWhiteSpace();
        reason.ShouldContain("limit");
    }
}
