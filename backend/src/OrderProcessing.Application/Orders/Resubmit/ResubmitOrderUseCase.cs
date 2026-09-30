using Microsoft.Extensions.Logging;
using OrderProcessing.Application.Orders.Payments;
using OrderProcessing.Domain.Orders;

namespace OrderProcessing.Application.Orders.Resubmit;

public sealed partial class ResubmitOrderUseCase(OrderPaymentProcessor payments, ILogger<ResubmitOrderUseCase> logger)
{
    public Task<OrderProcessingResult> ExecuteAsync(OrderNumber orderNumber, CancellationToken ct)
    {
        LogResubmitRequested(orderNumber.Value);
        return payments.ProcessAsync(orderNumber, ct);
    }
}
