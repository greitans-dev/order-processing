using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrderProcessing.Application.Orders.Listing;
using OrderProcessing.Application.Orders.Locking;
using OrderProcessing.Application.Orders.Payments;
using OrderProcessing.Application.Orders.Submit;

namespace OrderProcessing.Application;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton(new PaymentProcessingOptions());
        return services
            .AddSingleton<OrderLockRegistry>() // must be one instance per process
            .AddScoped<OrderPaymentProcessor>()
            .AddScoped<SubmitOrderUseCase>()
            .AddScoped<GetUserOrdersUseCase>();
    }
}
