using Microsoft.Extensions.DependencyInjection;
using OrderProcessing.Application.Abstractions;
using OrderProcessing.Domain.Payments;
using OrderProcessing.Infrastructure;
using OrderProcessing.Infrastructure.Payments;
using Shouldly;

namespace OrderProcessing.Infrastructure.Tests;

public class PaymentGatewayRegistryTests
{
    private static PaymentGatewayRegistry Registry() =>
        new([new MockGatewayAlpha(), new MockGatewayBeta()]);

    [Fact]
    public void Resolve_returns_gateway_by_id() =>
        Registry().Resolve(new PaymentGatewayId("mock-beta")).ShouldBeOfType<MockGatewayBeta>();

    [Fact]
    public void Resolve_unknown_throws() =>
        Should.Throw<UnknownPaymentGatewayException>(() => Registry().Resolve(new PaymentGatewayId("ghost")));

    [Fact]
    public void ListAvailable_returns_both_mocks() =>
        Registry().ListAvailable().Select(g => g.Id).ShouldBe(["mock-alpha", "mock-beta"], ignoreOrder: true);

    [Fact]
    public void Duplicate_gateway_ids_are_rejected() =>
        Should.Throw<InvalidOperationException>(() => new PaymentGatewayRegistry([new MockGatewayAlpha(), new MockGatewayAlpha()]));

    [Fact]
    public void AddInfrastructure_wires_registry_and_repository()
    {
        using var provider = new ServiceCollection().AddInfrastructure().BuildServiceProvider();
        provider.GetRequiredService<IPaymentGatewayRegistry>().ListAvailable().Count.ShouldBe(2);
        provider.GetRequiredService<IOrderRepository>()
            .ShouldBeSameAs(provider.GetRequiredService<IOrderRepository>());
    }
}
