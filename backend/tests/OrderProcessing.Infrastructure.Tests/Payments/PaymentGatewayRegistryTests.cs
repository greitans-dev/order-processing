using OrderProcessing.Application.Abstractions;
using OrderProcessing.Domain.Payments;
using OrderProcessing.Infrastructure.Payments;
using Shouldly;

namespace OrderProcessing.Infrastructure.Tests.Payments;

public class PaymentGatewayRegistryTests
{
    private static MockGateway Alpha() => new("mock-alpha", "Alpha", "ALPHA-");

    private static MockGateway Beta() => new("mock-beta", "Beta", "BETA-");

    private static PaymentGatewayRegistry Registry() =>
        new([Alpha(), Beta()]);

    [Fact]
    public void Resolve_KnownId_ReturnsMatchingGateway() =>
        Registry().Resolve(new PaymentGatewayId("mock-beta")).GatewayId.ShouldBe("mock-beta");

    [Fact]
    public void Resolve_UnknownId_Throws() =>
        Should.Throw<UnknownPaymentGatewayException>(() => Registry().Resolve(new PaymentGatewayId("ghost")));

    [Fact]
    public void ListAvailable_DefaultMocks_ReturnsBothGateways() =>
        Registry().ListAvailable().Select(g => g.Id).ShouldBe(["mock-alpha", "mock-beta"], ignoreOrder: true);

    [Fact]
    public void Constructor_DuplicateGatewayIds_Throws() =>
        Should.Throw<InvalidOperationException>(() => new PaymentGatewayRegistry([Alpha(), Alpha()]));
}
