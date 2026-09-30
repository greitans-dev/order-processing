using Microsoft.Extensions.Logging;
using OrderProcessing.Domain.Orders;

namespace OrderProcessing.Application.Orders;

public sealed partial class ResubmitOrderUseCase(OrderPaymentProcessor payments, ILogger<ResubmitOrderUseCase> logger)
{
    public Task<OrderProcessingResult> ExecuteAsync(OrderNumber orderNumber, CancellationToken ct)
    {
        LogResubmitRequested(orderNumber.Value);
        return payments.ProcessAsync(orderNumber, ct);
    }

    [LoggerMessage(EventId = 1003, Level = LogLevel.Information,
        Message = "Resubmit requested for order {OrderNumber}")]
    private partial void LogResubmitRequested(string orderNumber);
}
