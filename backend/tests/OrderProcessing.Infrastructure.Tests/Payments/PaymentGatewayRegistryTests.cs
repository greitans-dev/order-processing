using OrderProcessing.Application.Abstractions;
using OrderProcessing.Domain.Payments;
using OrderProcessing.Infrastructure.Payments;
using Shouldly;

namespace OrderProcessing.Infrastructure.Tests.Payments;

public class PaymentGatewayRegistryTests
{
    private static PaymentGatewayRegistry Registry() =>
        new([new MockGatewayAlpha(), new MockGatewayBeta()]);

    [Fact]
    public void Resolve_KnownId_ReturnsMatchingGateway() =>
        Registry().Resolve(new PaymentGatewayId("mock-beta")).ShouldBeOfType<MockGatewayBeta>();

    [Fact]
    public void Resolve_UnknownId_Throws() =>
        Should.Throw<UnknownPaymentGatewayException>(() => Registry().Resolve(new PaymentGatewayId("ghost")));

    [Fact]
    public void ListAvailable_DefaultMocks_ReturnsBothGateways() =>
        Registry().ListAvailable().Select(g => g.Id).ShouldBe(["mock-alpha", "mock-beta"], ignoreOrder: true);

    [Fact]
    public void Constructor_DuplicateGatewayIds_Throws() =>
        Should.Throw<InvalidOperationException>(() => new PaymentGatewayRegistry([new MockGatewayAlpha(), new MockGatewayAlpha()]));
}
