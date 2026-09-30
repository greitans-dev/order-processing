using System.Diagnostics;
using Microsoft.Extensions.Logging;
using OrderProcessing.Application.Abstractions;
using OrderProcessing.Domain.Orders;
using OrderProcessing.Domain.Payments;

namespace OrderProcessing.Application.Orders;

public sealed partial class OrderProcessingService(
    IOrderRepository repository,
    IPaymentGatewayRegistry gatewayRegistry,
    OrderNumberLockRegistry locks,
    ILogger<OrderProcessingService> logger)
{
    public async Task<OrderProcessingResult> SubmitNewOrderAsync(SubmitOrderCommand command, CancellationToken ct)
    {
        var gatewayId = new PaymentGatewayId(command.PaymentGatewayId);
        gatewayRegistry.Resolve(gatewayId); // fail fast: never persist an order for an unknown gateway
        var amount = Money.Of(command.PayableAmount, command.CurrencyCode);

        using var _ = await locks.AcquireAsync(command.UserId, command.IdempotencyKey, ct);
        var existingOrder = await repository.FindByIdempotencyKeyAsync(command.UserId, command.IdempotencyKey, ct);
        if (existingOrder is not null)
        {
            if (!existingOrder.MatchesRequest(command.UserId, amount, gatewayId, command.Description))
            {
                LogIdempotencyKeyConflict(command.IdempotencyKey.Value, command.UserId, existingOrder.OrderNumber.Value);
                throw new IdempotencyKeyReuseException(command.IdempotencyKey);
            }
            LogIdempotentReplay(command.UserId, existingOrder.OrderNumber.Value, existingOrder.Status.ToString());
            // Replay: a failed order is only retried through resubmit, never by repeating the key.
            return existingOrder.Status == OrderStatus.Failed
                ? OrderProcessingResult.Failure(existingOrder.OrderNumber.Value, existingOrder.FailureReason!)
                : await ProcessPaymentAsync(existingOrder.OrderNumber, ct);
        }

        var newOrder = Order.Create(
            command.UserId,
            command.IdempotencyKey,
            DateTimeOffset.UtcNow,
            amount,
            gatewayId,
            command.Description);
        await repository.AddAsync(newOrder, ct);
        LogOrderCreated(newOrder.OrderNumber.Value, newOrder.UserId, amount.Amount, amount.CurrencyCode, gatewayId.Value);
        return await ProcessPaymentAsync(newOrder.OrderNumber, ct);
    }

    public Task<OrderProcessingResult> ResubmitOrderAsync(OrderNumber orderNumber, CancellationToken ct)
    {
        LogResubmitRequested(orderNumber.Value);
        return ProcessPaymentAsync(orderNumber, ct);
    }

    public async Task<IReadOnlyList<OrderSummaryDto>> GetOrdersForUserAsync(string userId, CancellationToken ct)
    {
        var orders = await repository.FindByUserIdAsync(userId, ct);
        return orders
            .OrderByDescending(o => o.CreatedAtUtc)
            .ThenBy(o => o.OrderNumber.Value).Select(ToSummary)
            .ToList();
    }

    private async Task<OrderProcessingResult> ProcessPaymentAsync(OrderNumber orderNumber, CancellationToken ct)
    {
        using var _ = await locks.AcquireAsync(orderNumber, ct);
        var order = await repository.FindByOrderNumberAsync(orderNumber, ct)
            ?? throw new OrderNotFoundException(orderNumber);

        if (order.Status == OrderStatus.Paid)
        {
            LogAlreadyPaid(order.OrderNumber.Value);
            return OrderProcessingResult.AlreadyPaid(ToDto(order.Receipt!));
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
            return OrderProcessingResult.Success(ToDto(receipt));
        }

        LogChargeDeclined(order.OrderNumber.Value, order.PaymentGatewayId.Value, stopwatch.ElapsedMilliseconds,
            result.FailureReason!);
        order.MarkFailed(result.FailureReason!);
        await repository.UpdateAsync(order, ct);
        return OrderProcessingResult.Failure(order.OrderNumber.Value, result.FailureReason!);
    }

    private static OrderReceiptDto ToDto(Receipt r) =>
        new(r.OrderNumber.Value, r.PaidAmount.Amount, r.PaidAmount.CurrencyCode, r.PaidAtUtc, r.PaymentConfirmation);

    private static OrderSummaryDto ToSummary(Order o) =>
        new(o.OrderNumber.Value, o.PayableAmount.Amount, o.PayableAmount.CurrencyCode, o.PaymentGatewayId.Value,
            o.Description, o.Status.ToString(), o.FailureReason, o.Receipt is null ? null : ToDto(o.Receipt), o.CreatedAtUtc);
}
