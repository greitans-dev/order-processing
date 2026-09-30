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
    TimeProvider clock,
    PaymentProcessingOptions options,
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
        using var timeout = new CancellationTokenSource(options.GatewayTimeout);
        using var chargeToken = CancellationTokenSource.CreateLinkedTokenSource(ct, timeout.Token);
        try
        {
            result = await gateway.ChargeAsync(
                new PaymentRequest(order.OrderNumber, order.PayableAmount, order.Description), chargeToken.Token);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            // The outcome is unknown: the gateway may have charged. The order stays pending, so a retry charges again
            // and a real gateway must deduplicate on the order number.
            LogChargeTimedOut(order.OrderNumber.Value, order.PaymentGatewayId.Value, stopwatch.ElapsedMilliseconds);
            throw new PaymentGatewayTimeoutException(order.OrderNumber, order.PaymentGatewayId.Value,
                options.GatewayTimeout);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogChargeThrew(ex, order.OrderNumber.Value, order.PaymentGatewayId.Value);
            throw;
        }

        // The gateway has answered, so the outcome is saved with CancellationToken.None: a canceled request must not
        // leave a charged order Pending.
        if (result.IsSuccess)
        {
            LogChargeSucceeded(order.OrderNumber.Value, order.PaymentGatewayId.Value, stopwatch.ElapsedMilliseconds);
            var receipt = new Receipt(order.OrderNumber, order.PayableAmount, clock.GetUtcNow(),
                result.ConfirmationCode!);
            order.MarkPaid(receipt);
            await repository.UpdateAsync(order, CancellationToken.None);
            return OrderProcessingResult.Success(OrderDtoMapper.ToDto(receipt));
        }

        LogChargeDeclined(order.OrderNumber.Value, order.PaymentGatewayId.Value, stopwatch.ElapsedMilliseconds,
            result.FailureReason!);
        order.MarkFailed(result.FailureReason!);
        await repository.UpdateAsync(order, CancellationToken.None);
        return OrderProcessingResult.Failure(order.OrderNumber.Value, result.FailureReason!);
    }
}
