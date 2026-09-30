using System.Diagnostics;
using Microsoft.Extensions.Logging;
using OrderProcessing.Application.Abstractions;
using OrderProcessing.Application.Orders.Locking;
using OrderProcessing.Domain.Orders;
using OrderProcessing.Domain.Payments;

namespace OrderProcessing.Application.Orders.Payments;

public sealed partial class OrderPaymentProcessor(
    IOrderRepository repository,
    IPaymentGatewayRegistry gatewayRegistry,
    OrderNumberLockRegistry locks,
    ILogger<OrderPaymentProcessor> logger)
{
    // Takes the per-order lock itself. Callers may already hold the idempotency-key lock, never the other way round.
    public async Task<OrderProcessingResult> ProcessAsync(OrderNumber orderNumber, CancellationToken ct)
    {
        using var _ = await locks.AcquireAsync(orderNumber, ct);
        var order = await repository.FindByOrderNumberAsync(orderNumber, ct)
            ?? throw new OrderNotFoundException(orderNumber);

        if (order.Status == OrderStatus.Paid)
        {
            LogAlreadyPaid(order.OrderNumber.Value);
            return OrderProcessingResult.AlreadyPaid(OrderDtoMapper.ToDto(order.Receipt!));
        }

        var gateway = gatewayRegistry.Resolve(order.PaymentGatewayId);
        LogChargeStarted(order.OrderNumber.Value, order.PaymentGatewayId.Value);
        var stopwatch = Stopwatch.StartNew();
        PaymentResult result;
        try
        {
            result = await gateway.ChargeAsync(
                new PaymentRequest(order.OrderNumber, order.PayableAmount, order.Description), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogChargeThrew(ex, order.OrderNumber.Value, order.PaymentGatewayId.Value);
            throw;
        }

        if (result.IsSuccess)
        {
            LogChargeSucceeded(order.OrderNumber.Value, order.PaymentGatewayId.Value, stopwatch.ElapsedMilliseconds);
            var receipt = new Receipt(order.OrderNumber, order.PayableAmount, DateTimeOffset.UtcNow,
                result.ConfirmationCode!);
            order.MarkPaid(receipt);
            await repository.UpdateAsync(order, ct);
            return OrderProcessingResult.Success(OrderDtoMapper.ToDto(receipt));
        }

        LogChargeDeclined(order.OrderNumber.Value, order.PaymentGatewayId.Value, stopwatch.ElapsedMilliseconds,
            result.FailureReason!);
        order.MarkFailed(result.FailureReason!);
        await repository.UpdateAsync(order, ct);
        return OrderProcessingResult.Failure(order.OrderNumber.Value, result.FailureReason!);
    }
}
