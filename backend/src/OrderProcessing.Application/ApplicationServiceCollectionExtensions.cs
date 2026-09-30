using Microsoft.Extensions.DependencyInjection;
using OrderProcessing.Application.Orders;

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
