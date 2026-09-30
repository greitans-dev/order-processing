using Microsoft.Extensions.Logging;
using OrderProcessing.Application.Orders.Commands;
using OrderProcessing.Application.Orders.Payments;
using OrderProcessing.Application.Orders.Results;

namespace OrderProcessing.Application.Orders.Resubmit;

public sealed partial class ResubmitOrderUseCase(OrderPaymentProcessor payments, ILogger<ResubmitOrderUseCase> logger)
{
    public Task<OrderProcessingResult> ExecuteAsync(ResubmitOrderCommand command, CancellationToken ct)
    {
        LogResubmitRequested(command.OrderNumber.Value);
        return payments.ProcessAsync(command.OrderNumber, ct);
    }
}
