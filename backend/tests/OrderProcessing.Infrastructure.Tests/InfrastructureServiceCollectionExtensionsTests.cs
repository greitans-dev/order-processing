using Microsoft.Extensions.DependencyInjection;
using OrderProcessing.Application.Abstractions;
using Shouldly;

namespace OrderProcessing.Infrastructure.Tests;

public class InfrastructureServiceCollectionExtensionsTests
{
    [Fact]
    public void AddInfrastructure_Default_WiresRegistryAndSingletonRepository()
    {
        using var provider = new ServiceCollection().AddInfrastructure().BuildServiceProvider();
        provider.GetRequiredService<IPaymentGatewayRegistry>().ListAvailable().Count.ShouldBe(2);
        provider.GetRequiredService<IOrderRepository>()
            .ShouldBeSameAs(provider.GetRequiredService<IOrderRepository>());
    }
}
