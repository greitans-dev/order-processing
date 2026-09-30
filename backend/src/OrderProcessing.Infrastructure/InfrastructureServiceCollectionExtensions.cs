using Microsoft.Extensions.DependencyInjection;
using OrderProcessing.Application.Abstractions;
using OrderProcessing.Infrastructure.Payments;
using OrderProcessing.Infrastructure.Persistence;

namespace OrderProcessing.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        // One line per gateway: add or remove a gateway here, nothing else changes.
        services.AddSingleton<IPaymentGateway>(new MockGateway("mock-alpha", "Mock Gateway Alpha", "ALPHA-"));
        services.AddSingleton<IPaymentGateway>(new MockGateway("mock-beta", "Mock Gateway Beta", "BETA-"));
        services.AddSingleton<IPaymentGatewayRegistry, PaymentGatewayRegistry>();

        services.AddSingleton<IOrderRepository, InMemoryOrderRepository>();
        return services;
    }
}
