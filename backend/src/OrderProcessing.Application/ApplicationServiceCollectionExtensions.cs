using Microsoft.Extensions.DependencyInjection;
using OrderProcessing.Application.Orders.Listing;
using OrderProcessing.Application.Orders.Locking;
using OrderProcessing.Application.Orders.Payments;
using OrderProcessing.Application.Orders.Resubmit;
using OrderProcessing.Application.Orders.Submit;

namespace OrderProcessing.Application;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services) =>
        services
            .AddSingleton<OrderNumberLockRegistry>() // must be one instance per process
            .AddScoped<OrderPaymentProcessor>()
            .AddScoped<SubmitOrderUseCase>()
            .AddScoped<ResubmitOrderUseCase>()
            .AddScoped<GetUserOrdersUseCase>();
}
