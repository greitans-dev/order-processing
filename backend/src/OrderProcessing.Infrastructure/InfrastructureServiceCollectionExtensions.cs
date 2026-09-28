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
        services.AddSingleton<IPaymentGateway, MockGatewayAlpha>();
        services.AddSingleton<IPaymentGateway, MockGatewayBeta>();
        services.AddSingleton<IPaymentGatewayRegistry, PaymentGatewayRegistry>();

        services.AddSingleton<IOrderRepository, InMemoryOrderRepository>();
        return services;
    }
}
